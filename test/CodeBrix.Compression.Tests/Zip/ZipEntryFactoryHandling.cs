using CodeBrix.Compression.Zip;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;

namespace CodeBrix.Compression.Tests.Zip;

[TestFixture]
public class ZipEntryFactoryHandling : ZipBase
{
    // TODO: Complete testing for ZipEntryFactory

    // FileEntry creation and retrieval of information
    // DirectoryEntry creation and retrieval of information.

    [Test]
    [Category("Zip")]
    public void Defaults()
    {
        var testStart = DateTime.Now;
        var f = new ZipEntryFactory();
        ClassicAssert.IsNotNull(f.NameTransform);
        ClassicAssert.AreEqual(-1, f.GetAttributes);
        ClassicAssert.AreEqual(0, f.SetAttributes);
        ClassicAssert.AreEqual(ZipEntryFactory.TimeSetting.LastWriteTime, f.Setting);

        ClassicAssert.LessOrEqual(testStart, f.FixedDateTime);
        ClassicAssert.GreaterOrEqual(DateTime.Now, f.FixedDateTime);

        f = new ZipEntryFactory(ZipEntryFactory.TimeSetting.LastAccessTimeUtc);
        ClassicAssert.IsNotNull(f.NameTransform);
        ClassicAssert.AreEqual(-1, f.GetAttributes);
        ClassicAssert.AreEqual(0, f.SetAttributes);
        ClassicAssert.AreEqual(ZipEntryFactory.TimeSetting.LastAccessTimeUtc, f.Setting);
        ClassicAssert.LessOrEqual(testStart, f.FixedDateTime);
        ClassicAssert.GreaterOrEqual(DateTime.Now, f.FixedDateTime);

        var fixedDate = new DateTime(1999, 1, 2);
        f = new ZipEntryFactory(fixedDate);
        ClassicAssert.IsNotNull(f.NameTransform);
        ClassicAssert.AreEqual(-1, f.GetAttributes);
        ClassicAssert.AreEqual(0, f.SetAttributes);
        ClassicAssert.AreEqual(ZipEntryFactory.TimeSetting.Fixed, f.Setting);
        ClassicAssert.AreEqual(fixedDate, f.FixedDateTime);
    }

    [Test]
    [Category("Zip")]
    public void CreateInMemoryValues()
    {
        var tempFile = "bingo:";

        // Note the seconds returned will be even!
        var epochTime = new DateTime(1980, 1, 1);
        var createTime = new DateTime(2100, 2, 27, 11, 07, 56);
        var lastWriteTime = new DateTime(2050, 11, 3, 7, 23, 32);
        var lastAccessTime = new DateTime(2050, 11, 3, 0, 42, 12);

        var factory = new ZipEntryFactory();
        ZipEntry entry;
        int combinedAttributes;

        var startTime = DateTime.Now;

        factory.Setting = ZipEntryFactory.TimeSetting.CreateTime;
        factory.GetAttributes = ~((int)FileAttributes.ReadOnly);
        factory.SetAttributes = (int)FileAttributes.ReadOnly;
        combinedAttributes = (int)FileAttributes.ReadOnly;

        entry = factory.MakeFileEntry(tempFile, false);
        ClassicAssert.IsTrue(TestHelper.CompareDosDateTimes(startTime, entry.DateTime) <= 0, "Create time failure");
        ClassicAssert.AreEqual(entry.ExternalFileAttributes, combinedAttributes);
        ClassicAssert.AreEqual(-1, entry.Size);

        factory.FixedDateTime = startTime;
        factory.Setting = ZipEntryFactory.TimeSetting.Fixed;
        entry = factory.MakeFileEntry(tempFile, false);
        ClassicAssert.AreEqual(0, TestHelper.CompareDosDateTimes(startTime, entry.DateTime), "Access time failure");
        ClassicAssert.AreEqual(-1, entry.Size);

        factory.Setting = ZipEntryFactory.TimeSetting.LastWriteTime;
        entry = factory.MakeFileEntry(tempFile, false);
        ClassicAssert.IsTrue(TestHelper.CompareDosDateTimes(startTime, entry.DateTime) <= 0, "Write time failure");
        ClassicAssert.AreEqual(-1, entry.Size);
    }

    [Test]
    [Category("Zip")]
    [Category("CreatesTempFile")]
    [Platform("Win32NT")]
    public void CreatedFileEntriesUsesExpectedAttributes()
    {
        var tempDir = GetTempFilePath();
        if (tempDir == null)
        {
            Assert.Inconclusive("No permission to execute this test?");
        }

        tempDir = Path.Combine(tempDir, "SharpZipTest");
        Directory.CreateDirectory(tempDir);

        try
        {
            var tempFile = Path.Combine(tempDir, "SharpZipTest.Zip");
				
            using (var f = File.Create(tempFile, 1024))
            {
                f.WriteByte(0);
            }

            var attributes = FileAttributes.Hidden;

            File.SetAttributes(tempFile, attributes);
            ZipEntryFactory factory = null;
            ZipEntry entry;
            var combinedAttributes = 0;

            try
            {
                factory = new ZipEntryFactory();

                factory.GetAttributes = ~((int)FileAttributes.ReadOnly);
                factory.SetAttributes = (int)FileAttributes.ReadOnly;
                combinedAttributes = (int)(FileAttributes.ReadOnly | FileAttributes.Hidden);

                entry = factory.MakeFileEntry(tempFile);
                ClassicAssert.AreEqual(entry.ExternalFileAttributes, combinedAttributes);
                ClassicAssert.AreEqual(1, entry.Size);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
			
    }

    [Test]
    [Category("Zip")]
    [Category("CreatesTempFile")]
    [TestCase(ZipEntryFactory.TimeSetting.CreateTime)]
    [TestCase(ZipEntryFactory.TimeSetting.LastAccessTime)]
    [TestCase(ZipEntryFactory.TimeSetting.LastWriteTime)]
    public void CreatedFileEntriesUsesExpectedTime(ZipEntryFactory.TimeSetting timeSetting)
    {
        var tempDir = GetTempFilePath();
        if (tempDir == null)
        {
            Assert.Inconclusive("No permission to execute this test?");
        }

        tempDir = Path.Combine(tempDir, "SharpZipTest");

        // Note the seconds returned will be even!
        var expectedTime = new DateTime(2100, 2, 27, 11, 07, 56);

        Directory.CreateDirectory(tempDir);

        try
        {

            var tempFile = Path.Combine(tempDir, "SharpZipTest.Zip");
				
            using (var f = File.Create(tempFile, 1024))
            {
                f.WriteByte(0);
            }

            var fileTime = DateTime.MinValue;

            if (timeSetting == ZipEntryFactory.TimeSetting.CreateTime) {
                File.SetCreationTime(tempFile, expectedTime);
                fileTime = File.GetCreationTime(tempFile);
            }

            if (timeSetting == ZipEntryFactory.TimeSetting.LastAccessTime){
                File.SetLastAccessTime(tempFile, expectedTime);
                fileTime = File.GetLastAccessTime(tempFile);
            }

            if (timeSetting == ZipEntryFactory.TimeSetting.LastWriteTime) {
                File.SetLastWriteTime(tempFile, expectedTime);
                fileTime = File.GetLastWriteTime(tempFile);
            }

            if(fileTime != expectedTime) {
                Assert.Inconclusive("File time could not be altered");
            }

            var factory = new ZipEntryFactory();

            factory.Setting = timeSetting;

            var entry = factory.MakeFileEntry(tempFile);
            ClassicAssert.AreEqual(expectedTime, entry.DateTime);
            ClassicAssert.AreEqual(1, entry.Size);

        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
			
    }

    [Test]
    [Category("Zip")]
    [Category("CreatesTempFile")]
    [TestCase(ZipEntryFactory.TimeSetting.CreateTime)]
    [TestCase(ZipEntryFactory.TimeSetting.LastAccessTime)]
    [TestCase(ZipEntryFactory.TimeSetting.LastWriteTime)]
    public void CreatedDirectoryEntriesUsesExpectedTime(ZipEntryFactory.TimeSetting timeSetting)
    {
        var tempDir = GetTempFilePath();
        if (tempDir == null)
        {
            Assert.Inconclusive("No permission to execute this test?");
        }

        tempDir = Path.Combine(tempDir, "SharpZipTest");

        // Note the seconds returned will be even!
        var expectedTime = new DateTime(2100, 2, 27, 11, 07, 56);

        Directory.CreateDirectory(tempDir);

        try
        {

            var tempFile = Path.Combine(tempDir, "SharpZipTest.Zip");
				
            using (var f = File.Create(tempFile, 1024))
            {
                f.WriteByte(0);
            }

            var dirTime = DateTime.MinValue;

            if (timeSetting == ZipEntryFactory.TimeSetting.CreateTime) {
                Directory.SetCreationTime(tempFile, expectedTime);
                dirTime = Directory.GetCreationTime(tempDir);
            }

            if (timeSetting == ZipEntryFactory.TimeSetting.LastAccessTime){
                Directory.SetLastAccessTime(tempDir, expectedTime);
                dirTime = Directory.GetLastAccessTime(tempDir);
            }

            if (timeSetting == ZipEntryFactory.TimeSetting.LastWriteTime) {
                Directory.SetLastWriteTime(tempDir, expectedTime);
                dirTime = Directory.GetLastWriteTime(tempDir);
            }

            if(dirTime != expectedTime) {
                Assert.Inconclusive("Directory time could not be altered");
            }

            var factory = new ZipEntryFactory();

            factory.Setting = timeSetting;

            var entry = factory.MakeDirectoryEntry(tempDir);
            ClassicAssert.AreEqual(expectedTime, entry.DateTime);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
			
    }
}