using CodeBrix.Compression.Core;
using CodeBrix.Compression.Tests.TestSupport;
using CodeBrix.Compression.Zip;
using Xunit;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming

namespace CodeBrix.Compression.Tests.Zip;

public class ZipFileHandling : ZipBase
{
    [Fact]
    [Trait("Category", "Zip")]
    public void NullStreamDetected()
    {
        ZipFile bad = null;
        FileStream nullStream = null;

        var nullStreamDetected = false;

        try
        {
            // ReSharper disable once ExpressionIsAlwaysNull
            bad = new ZipFile(nullStream);
        }
        catch
        {
            nullStreamDetected = true;
        }

        Assert.True(nullStreamDetected, "Null stream should be detected in ZipFile constructor");
        Assert.Null(bad);
    }

    /// <summary>
    /// Check that adding too many entries is detected and handled
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void Zip64Entries()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        const int target = 65537;

        using var zipFile = ZipFile.Create(Path.GetTempFileName());
        zipFile.BeginUpdate();

        for (var i = 0; i < target; ++i)
        {
            var ze = new ZipEntry(i.ToString());
            ze.CompressedSize = 0;
            ze.Size = 0;
            zipFile.Add(ze);
        }
        zipFile.CommitUpdate();

        ZipTesting.AssertPassesTestArchive(zipFile);
        Assert.Equal(target, zipFile.Count);
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void EmbeddedArchive()
    {
        var memStream = new MemoryStream();
        using (var f = new ZipFile(memStream))
        {
            f.IsStreamOwner = false;

            var m = new StringMemoryDataSource("0000000");
            f.BeginUpdate(new MemoryArchiveStorage());
            f.Add(m, "a.dat");
            f.Add(m, "b.dat");
            f.CommitUpdate();
            ZipTesting.AssertPassesTestArchive(f);
        }

        var rawArchive = memStream.ToArray();
        var pseudoSfx = new byte[1049 + rawArchive.Length];
        Array.Copy(rawArchive, 0, pseudoSfx, 1049, rawArchive.Length);

        memStream = new MemoryStream(pseudoSfx);
        using (var f = new ZipFile(memStream))
        {
            for (var index = 0; index < f.Count; ++index)
            {
                var entryStream = f.GetInputStream(index);
                var data = new MemoryStream();
                StreamUtils.Copy(entryStream, data, new byte[128]);
                var contents = Encoding.ASCII.GetString(data.ToArray());
                Assert.Equal("0000000", contents);
            }
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void Zip64Useage()
    {
        var memStream = new MemoryStream();
        using (var f = new ZipFile(memStream))
        {
            f.IsStreamOwner = false;
            f.UseZip64 = UseZip64.On;

            var m = new StringMemoryDataSource("0000000");
            f.BeginUpdate(new MemoryArchiveStorage());
            f.Add(m, "a.dat");
            f.Add(m, "b.dat");
            f.CommitUpdate();
            ZipTesting.AssertPassesTestArchive(f);
        }

        var rawArchive = memStream.ToArray();

        var pseudoSfx = new byte[1049 + rawArchive.Length];
        Array.Copy(rawArchive, 0, pseudoSfx, 1049, rawArchive.Length);

        memStream = new MemoryStream(pseudoSfx);
        using (var f = new ZipFile(memStream))
        {
            for (var index = 0; index < f.Count; ++index)
            {
                var entryStream = f.GetInputStream(index);
                var data = new MemoryStream();
                StreamUtils.Copy(entryStream, data, new byte[128]);
                var contents = Encoding.ASCII.GetString(data.ToArray());
                Assert.Equal("0000000", contents);
            }
        }
    }

    /// <summary>
    /// Test that entries can be removed from a Zip64 file
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void Zip64Update()
    {
        using var memStream = new MemoryStream();
        using (var f = new ZipFile(memStream, leaveOpen: true))
        {
            f.UseZip64 = UseZip64.On;

            var m = new StringMemoryDataSource("0000000");
            f.BeginUpdate(new MemoryArchiveStorage());
            f.Add(m, "a.dat");
            f.Add(m, "b.dat");
            f.CommitUpdate();
            Assert.True(f.TestArchive(true));
        }

        memStream.Seek(0, SeekOrigin.Begin);

        using (var f = new ZipFile(memStream, leaveOpen: true))
        {
            Assert.Equal(2, f.Count);

            f.BeginUpdate(new MemoryArchiveStorage());
            f.Delete("b.dat");
            f.CommitUpdate();
            Assert.True(f.TestArchive(true));
        }

        memStream.Seek(0, SeekOrigin.Begin);

        using (var f = new ZipFile(memStream, leaveOpen: true))
        {
            Assert.Equal(1, f.Count);

            for (var index = 0; index < f.Count; ++index)
            {
                var entryStream = f.GetInputStream(index);
                var data = new MemoryStream();
                StreamUtils.Copy(entryStream, data, new byte[128]);
                var contents = Encoding.ASCII.GetString(data.ToArray());
                Assert.Equal("0000000", contents);
            }
        }
    }

    /// <summary>
    /// Test for issue #403 - zip64 locator signature bytes being present in a contained file,
    /// when the outer zip file isn't using zip64
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void FakeZip64Locator()
    {
        using var memStream = new MemoryStream();
        // set the file contents to the zip 64 directory locator signature
        var locatorValue = ZipConstants.Zip64CentralDirLocatorSignature;
        var locatorBytes = new byte[] { (byte)(locatorValue & 0xff), (byte)((locatorValue >> 8) & 0xff), (byte)((locatorValue >> 16) & 0xff), (byte)((locatorValue >> 24) & 0xff) };

        using (var f = new ZipFile(memStream, leaveOpen: true))
        {
            var m = new MemoryDataSource(locatorBytes);

            // Add the entry - set compression method to stored so the signature bytes remain as expected
            f.BeginUpdate(new MemoryArchiveStorage());
            f.Add(m, "a.dat", CompressionMethod.Stored);
            f.CommitUpdate();
            ZipTesting.AssertPassesTestArchive(f);
        }

        memStream.Seek(0, SeekOrigin.Begin);

        // Check that the archive is readable.
        using (var f = new ZipFile(memStream, leaveOpen: true))
        {
            Assert.Equal(1, f.Count);
        }
    }

    [Fact(Explicit = true)]
    [Trait("Category", "Zip")]
    public void Zip64Offset()
    {
        // TODO: Test to check that a zip64 offset value is loaded correctly.
        // Changes in ZipEntry to CentralHeaderRequiresZip64 and LocalHeaderRequiresZip64
        // were not quite correct...
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void BasicEncryption()
    {
        const string testValue = "0001000";
        var memStream = new MemoryStream();
        using (var zf = new ZipFile(memStream))
        {
            zf.IsStreamOwner = false;
            zf.Password = "Hello";

            var m = new StringMemoryDataSource(testValue);
            zf.BeginUpdate(new MemoryArchiveStorage());
            zf.Add(m, "a.dat");
            zf.CommitUpdate();
            Assert.True(zf.TestArchive(testData: true), "Archive test should pass");
        }

        using (var zf = new ZipFile(memStream))
        {
            zf.Password = "Hello";
            var ze = zf[0];

            Assert.True(ze.IsCrypted, "Entry should be encrypted");
            using (var r = new StreamReader(zf.GetInputStream(entryIndex: 0)))
            {
                var data = r.ReadToEnd();
                Assert.Equal(testValue, data);
            }
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void BasicEncryptionToDisk()
    {
        const string testValue = "0001000";
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");

        using (var zf = ZipFile.Create(tempFile))
        {
            zf.Password = "Hello";

            var m = new StringMemoryDataSource(testValue);
            zf.BeginUpdate();
            zf.Add(m, "a.dat");
            zf.CommitUpdate();
        }

        using (var zf = new ZipFile(tempFile))
        {
            zf.Password = "Hello";
            Assert.True(zf.TestArchive(testData: true), "Archive test should pass");
        }

        using (var zf = new ZipFile(tempFile))
        {
            zf.Password = "Hello";
            var ze = zf[0];

            Assert.True(ze.IsCrypted, "Entry should be encrypted");
            using (var r = new StreamReader(zf.GetInputStream(entryIndex: 0)))
            {
                var data = r.ReadToEnd();
                Assert.Equal(testValue, data);
            }
        }

        File.Delete(tempFile);
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void AddEncryptedEntriesToExistingArchive()
    {
        const string testValue = "0001000";
        var memStream = new MemoryStream();
        using (var f = new ZipFile(memStream))
        {
            f.IsStreamOwner = false;
            f.UseZip64 = UseZip64.Off;

            var m = new StringMemoryDataSource(testValue);
            f.BeginUpdate(new MemoryArchiveStorage());
            f.Add(m, "a.dat");
            f.CommitUpdate();
            Assert.True(f.TestArchive(true), "Archive test should pass");
        }

        using (var g = new ZipFile(memStream))
        {
            var ze = g[0];

            Assert.False(ze.IsCrypted, "Entry should NOT be encrypted");
            using (var r = new StreamReader(g.GetInputStream(0)))
            {
                var data = r.ReadToEnd();
                Assert.Equal(testValue, data);
            }

            var n = new StringMemoryDataSource(testValue);

            g.Password = "Axolotyl";
            g.UseZip64 = UseZip64.Off;
            g.IsStreamOwner = false;
            g.BeginUpdate();
            g.Add(n, "a1.dat");
            g.CommitUpdate();
            Assert.True(g.TestArchive(true), "Archive test should pass");
            ze = g[1];
            Assert.True(ze.IsCrypted, "New entry should be encrypted");

            using (var r = new StreamReader(g.GetInputStream(0)))
            {
                var data = r.ReadToEnd();
                Assert.Equal(testValue, data);
            }
        }
    }

    private void TryDeleting(byte[] master, int totalEntries, int additions, params string[] toDelete)
    {
        var ms = new MemoryStream();
        ms.Write(master, 0, master.Length);

        using var f = new ZipFile(ms);
        f.IsStreamOwner = false;
        Assert.Equal(totalEntries, f.Count);
        ZipTesting.AssertPassesTestArchive(f);
        f.BeginUpdate(new MemoryArchiveStorage());

        for (var i = 0; i < additions; ++i)
        {
            f.Add(new StringMemoryDataSource("Another great file"),
                string.Format("Add{0}.dat", i + 1));
        }

        foreach (var name in toDelete)
        {
            f.Delete(name);
        }
        f.CommitUpdate();

        // write stream to file to assist debugging.
        // WriteToFile(@"c:\aha.zip", ms.ToArray());

        var newTotal = totalEntries + additions - toDelete.Length;
        Assert.Equal(newTotal, f.Count);
        Assert.True(f.TestArchive(true), "Archive test should pass");
    }

    private void TryDeleting(byte[] master, int totalEntries, int additions, params int[] toDelete)
    {
        var ms = new MemoryStream();
        ms.Write(master, 0, master.Length);

        using var f = new ZipFile(ms);
        f.IsStreamOwner = false;
        Assert.Equal(totalEntries, f.Count);
        ZipTesting.AssertPassesTestArchive(f);
        f.BeginUpdate(new MemoryArchiveStorage());

        for (var i = 0; i < additions; ++i)
        {
            f.Add(new StringMemoryDataSource("Another great file"),
                string.Format("Add{0}.dat", i + 1));
        }

        foreach (var i in toDelete)
        {
            f.Delete(f[i]);
        }
        f.CommitUpdate();

        /* write stream to file to assist debugging.
                            byte[] data = ms.ToArray();
                            using ( FileStream fs = File.Open(@"c:\aha.zip", FileMode.Create, FileAccess.ReadWrite, FileShare.Read) ) {
                                fs.Write(data, 0, data.Length);
                            }
            */
        var newTotal = totalEntries + additions - toDelete.Length;
        Assert.Equal(newTotal, f.Count);
        Assert.True(f.TestArchive(true), "Archive test should pass");
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void AddAndDeleteEntriesMemory()
    {
        var memStream = new MemoryStream();

        using (var f = new ZipFile(memStream))
        {
            f.IsStreamOwner = false;

            f.BeginUpdate(new MemoryArchiveStorage());
            f.Add(new StringMemoryDataSource("Hello world"), Utils.SystemRoot + @"a\a.dat");
            f.Add(new StringMemoryDataSource("Another"), @"\b\b.dat");
            f.Add(new StringMemoryDataSource("Mr C"), @"c\c.dat");
            f.Add(new StringMemoryDataSource("Mrs D was a star"), @"d\d.dat");
            f.CommitUpdate();
            ZipTesting.AssertPassesTestArchive(f);
            foreach (var entry in f)
            {
                Console.WriteLine($" - {entry.Name}");
            }
        }

        var master = memStream.ToArray();

        TryDeleting(master, 4, 1, Utils.SystemRoot + @"a\a.dat");
        TryDeleting(master, 4, 1, @"\a\a.dat");
        TryDeleting(master, 4, 1, @"a/a.dat");

        TryDeleting(master, 4, 0, 0);
        TryDeleting(master, 4, 0, 1);
        TryDeleting(master, 4, 0, 2);
        TryDeleting(master, 4, 0, 3);
        TryDeleting(master, 4, 0, 0, 1);
        TryDeleting(master, 4, 0, 0, 2);
        TryDeleting(master, 4, 0, 0, 3);
        TryDeleting(master, 4, 0, 1, 2);
        TryDeleting(master, 4, 0, 1, 3);
        TryDeleting(master, 4, 0, 2);

        TryDeleting(master, 4, 1, 0);
        TryDeleting(master, 4, 1, 1);
        TryDeleting(master, 4, 3, 2);
        TryDeleting(master, 4, 4, 3);
        TryDeleting(master, 4, 10, 0, 1);
        TryDeleting(master, 4, 10, 0, 2);
        TryDeleting(master, 4, 10, 0, 3);
        TryDeleting(master, 4, 20, 1, 2);
        TryDeleting(master, 4, 30, 1, 3);
        TryDeleting(master, 4, 40, 2);
    }

    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void AddAndDeleteEntries()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        var addFile = Path.Combine(tempFile, "a.dat");
        MakeTempFile(addFile, 1);

        var addFile2 = Path.Combine(tempFile, "b.dat");
        MakeTempFile(addFile2, 259);

        var addDirectory = Path.Combine(tempFile, "dir");

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");

        using (var f = ZipFile.Create(tempFile))
        {
            f.BeginUpdate();
            f.Add(addFile);
            f.Add(addFile2);
            f.AddDirectory(addDirectory);
            f.CommitUpdate();
            ZipTesting.AssertPassesTestArchive(f);
        }

        using (var f = new ZipFile(tempFile))
        {
            Assert.Equal(3, f.Count);
            ZipTesting.AssertPassesTestArchive(f);

            // Delete file
            f.BeginUpdate();
            f.Delete(f[0]);
            f.CommitUpdate();
            Assert.Equal(2, f.Count);
            ZipTesting.AssertPassesTestArchive(f);

            // Delete directory
            f.BeginUpdate();
            f.Delete(f[1]);
            f.CommitUpdate();
            Assert.Equal(1, f.Count);
            ZipTesting.AssertPassesTestArchive(f);
        }

        File.Delete(addFile);
        File.Delete(addFile2);
        File.Delete(tempFile);
    }

    /// <summary>
    /// Simple round trip test for ZipFile class
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void RoundTrip()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");

        try
        {
            MakeZipFile(tempFile, "", 10, 1024, "");

            using var zipFile = new ZipFile(tempFile);
            foreach (var e in zipFile)
            {
                var instream = zipFile.GetInputStream(e);
                CheckKnownEntry(instream, 1024);
            }
            zipFile.Close();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Simple round trip test for ZipFile class
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void RoundTripInMemory()
    {
        var storage = new MemoryStream();
        MakeZipFile(storage, false, "", 10, 1024, "");

        using var zipFile = new ZipFile(storage);
        foreach (var e in zipFile)
        {
            var instream = zipFile.GetInputStream(e);
            CheckKnownEntry(instream, 1024);
        }
        zipFile.Close();
    }

    /// <summary>
    /// Simple async round trip test for ZipFile class
    /// </summary>
    [Theory]
    [InlineData(CompressionMethod.Stored)]
    [InlineData(CompressionMethod.Deflated)]
    [InlineData(CompressionMethod.BZip2)]
    [Trait("Category", "Zip")]
    [Trait("Category", "Async")]
    public async Task RoundTripInMemoryAsync(CompressionMethod compressionMethod)
    {
        var storage = new MemoryStream();
        MakeZipFile(storage, compressionMethod, false, "", 10, 1024, "");

        using var zipFile = new ZipFile(storage);
        foreach (var e in zipFile)
        {
            var instream = zipFile.GetInputStream(e);
            await CheckKnownEntryAsync(instream, 1024);
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void AddToEmptyArchive()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        var addFile = Path.Combine(tempFile, "a.dat");

        MakeTempFile(addFile, 1);

        try
        {
            tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");

            using (var f = ZipFile.Create(tempFile))
            {
                f.BeginUpdate();
                f.Add(addFile);
                f.CommitUpdate();
                Assert.Equal(1, f.Count);
                ZipTesting.AssertPassesTestArchive(f);
            }

            using (var f = new ZipFile(tempFile))
            {
                Assert.Equal(1, f.Count);
                f.BeginUpdate();
                f.Delete(f[0]);
                f.CommitUpdate();
                Assert.Equal(0, f.Count);
                ZipTesting.AssertPassesTestArchive(f);
                f.Close();
            }

            File.Delete(tempFile);
        }
        finally
        {
            File.Delete(addFile);
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void CreateEmptyArchive()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");

        using (var f = ZipFile.Create(tempFile))
        {
            f.BeginUpdate();
            f.CommitUpdate();
            ZipTesting.AssertPassesTestArchive(f);
            f.Close();
        }

        using (var f = new ZipFile(tempFile))
        {
            Assert.Equal(0, f.Count);
        }

        File.Delete(tempFile);
    }

    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void CreateArchiveWithNoCompression()
    {
        using var sourceFile = Utils.GetDummyFile();
        using var zipFile = Utils.GetDummyFile(0);
        var inputContent = File.ReadAllText(sourceFile);
        using (var zf = ZipFile.Create(zipFile))
        {
            zf.BeginUpdate();
            zf.Add(sourceFile, CompressionMethod.Stored);
            zf.CommitUpdate();
            ZipTesting.AssertPassesTestArchive(zf);
            zf.Close();
        }

        using (var zf = new ZipFile(zipFile))
        {
            Assert.Equal(1, zf.Count);
            using (var sr = new StreamReader(zf.GetInputStream(zf[0])))
            {
                var outputContent = sr.ReadToEnd();
                Assert.Equal(inputContent, outputContent);
            }
        }
    }

    /// <summary>
    /// Check that ZipFile finds entries when its got a long comment
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void FindEntriesInArchiveWithLongComment()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");
        var longComment = new String('A', 65535);
        MakeZipFile(tempFile, "", 1, 1, longComment);

        try
        {
            using var zipFile = new ZipFile(tempFile);
            foreach (var e in zipFile)
            {
                var instream = zipFile.GetInputStream(e);
                CheckKnownEntry(instream, 1);
            }
            zipFile.Close();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Check that ZipFile doesnt find entries when there is more than 64K of data at the end.
    /// </summary>
    /// <remarks>
    /// This may well be flawed but is the current behaviour.
    /// </remarks>
    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void FindEntriesInArchiveExtraData()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");
        var longComment = new String('A', 65535);
        var tempStream = File.Create(tempFile);
        MakeZipFile(tempStream, false, "", 1, 1, longComment);

        tempStream.WriteByte(85);
        tempStream.Close();

        var fails = false;
        try
        {
            using var zipFile = new ZipFile(tempFile);
            foreach (var e in zipFile)
            {
                var instream = zipFile.GetInputStream(e);
                CheckKnownEntry(instream, 1);
            }
            zipFile.Close();
        }
        catch
        {
            fails = true;
        }

        File.Delete(tempFile);
        Assert.True(fails, "Currently zip file wont be found");
    }

    /// <summary>
    /// Test ZipFile Find method operation
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void FindEntry()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");
        MakeZipFile(tempFile, new string[] { "Farriera", "Champagne", "Urban myth" }, 10, "Aha");

        using (var zipFile = new ZipFile(tempFile))
        {
            Assert.Equal(3, zipFile.Count);

            var testIndex = zipFile.FindEntry("Farriera", false);
            Assert.Equal(0, testIndex);
            Assert.True(string.Compare(zipFile[testIndex].Name, "Farriera", StringComparison.Ordinal) == 0);

            testIndex = zipFile.FindEntry("Farriera", true);
            Assert.Equal(0, testIndex);
            Assert.True(string.Compare(zipFile[testIndex].Name, "Farriera", StringComparison.OrdinalIgnoreCase) == 0);

            testIndex = zipFile.FindEntry("urban mYTH", false);
            Assert.Equal(-1, testIndex);

            testIndex = zipFile.FindEntry("urban mYTH", true);
            Assert.Equal(2, testIndex);
            Assert.True(string.Compare(zipFile[testIndex].Name, "urban mYTH", StringComparison.OrdinalIgnoreCase) == 0);

            testIndex = zipFile.FindEntry("Champane.", false);
            Assert.Equal(-1, testIndex);

            testIndex = zipFile.FindEntry("Champane.", true);
            Assert.Equal(-1, testIndex);

            zipFile.Close();
        }
        File.Delete(tempFile);
    }

    /// <summary>
    /// Check that ZipFile class handles no entries in zip file
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void HandlesNoEntries()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");
        MakeZipFile(tempFile, "", 0, 1, "Aha");

        using (var zipFile = new ZipFile(tempFile))
        {
            Assert.Equal(0, zipFile.Count);
            zipFile.Close();
        }

        File.Delete(tempFile);
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void ArchiveTesting()
    {
        byte[] originalData = null;
        var compressedData = MakeInMemoryZip(ref originalData, CompressionMethod.Deflated,
            6, 1024, null, true);

        var ms = new MemoryStream(compressedData);
        ms.Seek(0, SeekOrigin.Begin);

        using (var testFile = new ZipFile(ms))
        {
            ZipTesting.AssertPassesTestArchive(testFile);

            var corrupted = new byte[compressedData.Length];
            Array.Copy(compressedData, corrupted, compressedData.Length);

            corrupted[123] = (byte)(~corrupted[123] & 0xff);
            ms = new MemoryStream(corrupted);
        }

        using (var testFile = new ZipFile(ms))
        {
            // Expect the archive to fail (invert logic of PassTestArchive)
            var report = new TestArchiveReport();
            var passed = testFile.TestArchive(true, TestStrategy.FindAllErrors, report.HandleTestResults);
            Assert.False(passed, "Error in archive not detected");
        }
    }

    private void TestDirectoryEntryImpl(MemoryStream s)
    {
        var outStream = new ZipOutputStream(s);
        outStream.IsStreamOwner = false;
        outStream.PutNextEntry(new ZipEntry("YeOldeDirectory/"));
        outStream.Close();

        var ms2 = new MemoryStream(s.ToArray());
        using var zf = new ZipFile(ms2);
        ZipTesting.AssertPassesTestArchive(zf);
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void TestDirectoryEntry()
    {
        TestDirectoryEntryImpl(new MemoryStream());
        TestDirectoryEntryImpl(new MemoryStreamWithoutSeek());
    }

    private void TestEncryptedDirectoryEntryImpl(MemoryStream s, int aesKeySize)
    {
        var outStream = new ZipOutputStream(s);
        outStream.Password = "Tonto hand me a beer";

        outStream.IsStreamOwner = false;
        outStream.PutNextEntry(new ZipEntry("YeUnreadableDirectory/") { AESKeySize = aesKeySize } );
        outStream.Close();

        var ms2 = new MemoryStream(s.ToArray());
        using var zf = new ZipFile(ms2);
        ZipTesting.AssertPassesTestArchive(zf);
    }

    [Theory]
    [Trait("Category", "Zip")]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(256)]
    public void TestEncryptedDirectoryEntry(int aesKeySize)
    {
        TestEncryptedDirectoryEntryImpl(new MemoryStream(), aesKeySize);
        TestEncryptedDirectoryEntryImpl(new MemoryStreamWithoutSeek(), aesKeySize);
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void Crypto_AddEncryptedEntryToExistingArchiveSafe()
    {
        var ms = new MemoryStream();

        byte[] rawData;

        using (var testFile = new ZipFile(ms))
        {
            testFile.IsStreamOwner = false;
            testFile.BeginUpdate();
            testFile.Add(new StringMemoryDataSource("Aha"), "No1", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("And so it goes"), "No2", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("No3"), "No3", CompressionMethod.Stored);
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);
            rawData = ms.ToArray();
        }

        ms = new MemoryStream(rawData);

        using (var testFile = new ZipFile(ms))
        {
            ZipTesting.AssertPassesTestArchive(testFile);

            testFile.BeginUpdate(new MemoryArchiveStorage(FileUpdateMode.Safe));
            testFile.Password = "pwd";
            testFile.Add(new StringMemoryDataSource("Zapata!"), "encrypttest.xml");
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);

            var entryIndex = testFile.FindEntry("encrypttest.xml", true);
            Assert.True(entryIndex >= 0);
            Assert.True(testFile[entryIndex].IsCrypted);
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void Crypto_AddEncryptedEntryToExistingArchiveDirect()
    {
        var ms = new MemoryStream();

        using (var testFile = new ZipFile(ms))
        {
            testFile.IsStreamOwner = false;
            testFile.BeginUpdate();
            testFile.Add(new StringMemoryDataSource("Aha"), "No1", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("And so it goes"), "No2", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("No3"), "No3", CompressionMethod.Stored);
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);
        }

        using (var testFile = new ZipFile(ms))
        {
            ZipTesting.AssertPassesTestArchive(testFile);
            testFile.IsStreamOwner = true;

            testFile.BeginUpdate();
            testFile.Password = "pwd";
            testFile.Add(new StringMemoryDataSource("Zapata!"), "encrypttest.xml");
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);

            var entryIndex = testFile.FindEntry("encrypttest.xml", true);
            Assert.True(entryIndex >= 0);
            Assert.True(testFile[entryIndex].IsCrypted);
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "Unicode")]
    public void UnicodeNames()
    {
        using var memStream = new MemoryStream();
        using (var f = new ZipFile(memStream))
        {
            f.IsStreamOwner = false;

            f.BeginUpdate(new MemoryArchiveStorage());
            foreach (var (language, name, _) in StringTesting.TestSamples)
            {
                f.Add(new StringMemoryDataSource(language), name,
                    CompressionMethod.Deflated, useUnicodeText: true);
            }
            f.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(f);
        }
        memStream.Seek(0, SeekOrigin.Begin);
        using (var zf = new ZipFile(memStream))
        {
            foreach (var name in StringTesting.Filenames)
            {
                string content;
                var index = zf.FindEntry(name, ignoreCase: true);
                var entry = zf[index];

                using (var entryStream = zf.GetInputStream(entry))
                using (var sr = new StreamReader(entryStream))
                {
                    content = sr.ReadToEnd();
                }

                Console.WriteLine($"Entry #{index}: {name}, Content: {content}");

                Assert.True(index >= 0);
                Assert.Equal(name, entry.Name);
            }
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void UpdateCommentOnlyInMemory()
    {
        var ms = new MemoryStream();

        using (var testFile = new ZipFile(ms))
        {
            testFile.IsStreamOwner = false;
            testFile.BeginUpdate();
            testFile.Add(new StringMemoryDataSource("Aha"), "No1", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("And so it goes"), "No2", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("No3"), "No3", CompressionMethod.Stored);
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);
        }

        using (var testFile = new ZipFile(ms))
        {
            ZipTesting.AssertPassesTestArchive(testFile);
            Assert.Equal("", testFile.ZipFileComment);
            testFile.IsStreamOwner = false;

            testFile.BeginUpdate();
            testFile.SetComment("Here is my comment");
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);
        }

        using (var testFile = new ZipFile(ms))
        {
            ZipTesting.AssertPassesTestArchive(testFile);
            Assert.Equal("Here is my comment", testFile.ZipFileComment);
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void UpdateCommentOnlyOnDisk()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }

        using (var testFile = ZipFile.Create(tempFile))
        {
            testFile.BeginUpdate();
            testFile.Add(new StringMemoryDataSource("Aha"), "No1", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("And so it goes"), "No2", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("No3"), "No3", CompressionMethod.Stored);
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);
        }

        using (var testFile = new ZipFile(tempFile))
        {
            ZipTesting.AssertPassesTestArchive(testFile);
            Assert.Equal("", testFile.ZipFileComment);

            testFile.BeginUpdate(new DiskArchiveStorage(testFile, FileUpdateMode.Direct));
            testFile.SetComment("Here is my comment");
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);
        }

        using (var testFile = new ZipFile(tempFile))
        {
            ZipTesting.AssertPassesTestArchive(testFile);
            Assert.Equal("Here is my comment", testFile.ZipFileComment);
        }
        File.Delete(tempFile);

        // Variant using indirect updating.
        using (var testFile = ZipFile.Create(tempFile))
        {
            testFile.BeginUpdate();
            testFile.Add(new StringMemoryDataSource("Aha"), "No1", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("And so it goes"), "No2", CompressionMethod.Stored);
            testFile.Add(new StringMemoryDataSource("No3"), "No3", CompressionMethod.Stored);
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);
        }

        using (var testFile = new ZipFile(tempFile))
        {
            ZipTesting.AssertPassesTestArchive(testFile);
            Assert.Equal("", testFile.ZipFileComment);

            testFile.BeginUpdate();
            testFile.SetComment("Here is my comment");
            testFile.CommitUpdate();

            ZipTesting.AssertPassesTestArchive(testFile);
        }

        using (var testFile = new ZipFile(tempFile))
        {
            ZipTesting.AssertPassesTestArchive(testFile);
            Assert.Equal("Here is my comment", testFile.ZipFileComment);
        }
        File.Delete(tempFile);
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void NameFactory()
    {
        var memStream = new MemoryStream();
        var fixedTime = new DateTime(1981, 4, 3);
        using var f = new ZipFile(memStream);
        f.IsStreamOwner = false;
        ((ZipEntryFactory)f.EntryFactory).IsUnicodeText = true;
        ((ZipEntryFactory)f.EntryFactory).Setting = ZipEntryFactory.TimeSetting.Fixed;
        ((ZipEntryFactory)f.EntryFactory).FixedDateTime = fixedTime;
        ((ZipEntryFactory)f.EntryFactory).SetAttributes = 1;
        f.BeginUpdate(new MemoryArchiveStorage());

        var names = new string[]
        {
            "\u030A\u03B0",     // Greek
            "\u0680\u0685"      // Arabic
        };

        foreach (var name in names)
        {
            f.Add(new StringMemoryDataSource("Hello world"), name,
                CompressionMethod.Deflated, true);
        }
        f.CommitUpdate();
        ZipTesting.AssertPassesTestArchive(f);

        foreach (var name in names)
        {
            var index = f.FindEntry(name, true);

            Assert.True(index >= 0);
            var found = f[index];
            Assert.Equal(name, found.Name);
            Assert.True(found.IsUnicodeText);
            Assert.Equal(fixedTime, found.DateTime);
            Assert.True(found.IsDOSEntry);
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void NestedArchive()
    {
        var ms = new MemoryStream();
        using (var zos = new ZipOutputStream(ms))
        {
            zos.IsStreamOwner = false;
            var ze = new ZipEntry("Nest1");

            zos.PutNextEntry(ze);
            var toWrite = Encoding.ASCII.GetBytes("Hello");
            zos.Write(toWrite, 0, toWrite.Length);
        }

        var data = ms.ToArray();

        ms = new MemoryStream();
        using (var zos = new ZipOutputStream(ms))
        {
            zos.IsStreamOwner = false;
            var ze = new ZipEntry("Container");
            ze.CompressionMethod = CompressionMethod.Stored;
            zos.PutNextEntry(ze);
            zos.Write(data, 0, data.Length);
        }

        using (var zipFile = new ZipFile(ms))
        {
            var e = zipFile[0];
            Assert.Equal("Container", e.Name);

            using (var nested = new ZipFile(zipFile.GetInputStream(0)))
            {
                ZipTesting.AssertPassesTestArchive(nested);
                Assert.Equal(1, nested.Count);

                var nestedStream = nested.GetInputStream(0);

                var reader = new StreamReader(nestedStream);

                var contents = reader.ReadToEnd();

                Assert.Equal("Hello", contents);
            }
        }
    }

    private Stream GetPartialStream()
    {
        var ms = new MemoryStream();
        using (var zos = new ZipOutputStream(ms))
        {
            zos.IsStreamOwner = false;
            var ze = new ZipEntry("E1");

            zos.PutNextEntry(ze);
            var toWrite = Encoding.ASCII.GetBytes("Hello");
            zos.Write(toWrite, 0, toWrite.Length);
        }

        var zf = new ZipFile(ms);

        return zf.GetInputStream(0);
    }

    [Fact]
    public void UnreferencedZipFileClosingPartialStream()
    {
        var s = GetPartialStream();

        GC.Collect();

        s.ReadByte();
    }

    /// <summary>
    /// Check that input stream is closed when IsStreamOwner is true (default), or leaveOpen is false
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void StreamClosedWhenOwner()
    {
        var ms = new MemoryStream();
        MakeZipFile(ms, false, "StreamClosedWhenOwner", 1, 10, "test");
        ms.Seek(0, SeekOrigin.Begin);
        var zipData = ms.ToArray();

        // Stream should be closed when leaveOpen is unspecified
        {
            var inMemoryZip = new TrackedMemoryStream(zipData);
            Assert.False(inMemoryZip.IsClosed, "Input stream should NOT be closed");

            using (var zipFile = new ZipFile(inMemoryZip))
            {
                Assert.True(zipFile.IsStreamOwner, "Should be stream owner by default");
            }

            Assert.True(inMemoryZip.IsClosed, "Input stream should be closed by default");
        }

        // Stream should be closed when leaveOpen is false
        {
            var inMemoryZip = new TrackedMemoryStream(zipData);
            Assert.False(inMemoryZip.IsClosed, "Input stream should NOT be closed");

            using (var zipFile = new ZipFile(inMemoryZip, false))
            {
                Assert.True(zipFile.IsStreamOwner, "Should be stream owner when leaveOpen is false");
            }

            Assert.True(inMemoryZip.IsClosed, "Input stream should be closed when leaveOpen is false");
        }
    }

    /// <summary>
    /// Check that input stream is not closed when IsStreamOwner is false;
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void StreamNotClosedWhenNotOwner()
    {
        var ms = new TrackedMemoryStream();
        MakeZipFile(ms, false, "StreamNotClosedWhenNotOwner", 1, 10, "test");
        ms.Seek(0, SeekOrigin.Begin);

        Assert.False(ms.IsClosed, "Input stream should NOT be closed");

        // Stream should not be closed when leaveOpen is true
        {
            using (var zipFile = new ZipFile(ms, true))
            {
                Assert.False(zipFile.IsStreamOwner, "Should NOT be stream owner when leaveOpen is true");
            }

            Assert.False(ms.IsClosed, "Input stream should NOT be closed when leaveOpen is true");
        }

        ms.Seek(0, SeekOrigin.Begin);

        // Stream should not be closed when IsStreamOwner is set to false after opening
        {
            using (var zipFile = new ZipFile(ms, false))
            {
                Assert.True(zipFile.IsStreamOwner, "Should be stream owner when leaveOpen is false");
                zipFile.IsStreamOwner = false;
                Assert.False(zipFile.IsStreamOwner, "Should be able to set IsStreamOwner to false");
            }

            Assert.False(ms.IsClosed, "Input stream should NOT be closed when IsStreamOwner is false");
        }
    }

    /// <summary>
    /// Check that input file is closed when IsStreamOwner is true (default), or leaveOpen is false
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void FileStreamClosedWhenOwner()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipFileStreamClosedWhenOwnerTest.Zip");
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }

        MakeZipFile(tempFile, "FileStreamClosedWhenOwner", 2, 10, "test");

        // Stream should be closed when leaveOpen is unspecified
        {
            var fileStream = new TrackedFileStream(tempFile);
            Assert.False(fileStream.IsClosed, "Input file should NOT be closed");

            using (var zipFile = new ZipFile(fileStream))
            {
                Assert.True(zipFile.IsStreamOwner, "Should be stream owner by default");
            }

            Assert.True(fileStream.IsClosed, "Input stream should be closed by default");
        }

        // Stream should be closed when leaveOpen is false
        {
            var fileStream = new TrackedFileStream(tempFile);
            Assert.False(fileStream.IsClosed, "Input stream should NOT be closed");

            using (var zipFile = new ZipFile(fileStream, false))
            {
                Assert.True(zipFile.IsStreamOwner, "Should be stream owner when leaveOpen is false");
            }

            Assert.True(fileStream.IsClosed, "Input stream should be closed when leaveOpen is false");
        }

        File.Delete(tempFile);
    }

    /// <summary>
    /// Check that input file is not closed when IsStreamOwner is false;
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void FileStreamNotClosedWhenNotOwner()
    {
        var tempFile = GetTempFilePath();
        Assert.NotNull(tempFile);

        tempFile = Path.Combine(tempFile, "SharpZipFileStreamNotClosedWhenNotOwner.Zip");
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }

        MakeZipFile(tempFile, "FileStreamClosedWhenOwner", 2, 10, "test");

        // Stream should not be closed when leaveOpen is true
        {
            using var fileStream = new TrackedFileStream(tempFile);
            Assert.False(fileStream.IsClosed, "Input file should NOT be closed");

            using (var zipFile = new ZipFile(fileStream, true))
            {
                Assert.False(zipFile.IsStreamOwner, "Should NOT be stream owner when leaveOpen is true");
            }

            Assert.False(fileStream.IsClosed, "Input stream should NOT be closed when leaveOpen is true");
        }

        // Stream should not be closed when IsStreamOwner is set to false after opening
        {
            using var fileStream = new TrackedFileStream(tempFile);
            Assert.False(fileStream.IsClosed, "Input file should NOT be closed");

            using (var zipFile = new ZipFile(fileStream, false))
            {
                Assert.True(zipFile.IsStreamOwner, "Should be stream owner when leaveOpen is false");
                zipFile.IsStreamOwner = false;
                Assert.False(zipFile.IsStreamOwner, "Should be able to set IsStreamOwner to false");
            }

            Assert.False(fileStream.IsClosed, "Input stream should NOT be closed when leaveOpen is true");
        }

        File.Delete(tempFile);
    }

    /// <summary>
    /// Check that input stream is only closed when construction fails and leaveOpen is false
    /// </summary>
    [Theory]
    [Trait("Category", "Zip")]
    [InlineData(true)]
    [InlineData(false)]
    public void StreamClosedOnError(bool leaveOpen)
    {
        var ms = new TrackedMemoryStream(new byte[32]);

        Assert.False(ms.IsClosed, "Underlying stream should NOT be closed initially");
        Assert.Throws<ZipException>(() =>
        {
            using var zf = new ZipFile(ms, leaveOpen);
        });

        if (leaveOpen)
        {
            Assert.False(ms.IsClosed, "Underlying stream should NOT be closed");
        }
        else
        {
            Assert.True(ms.IsClosed, "Underlying stream should be closed");
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void HostSystemPersistedFromOutputStream()
    {
        using var ms = new MemoryStream();
        var fileName = "testfile";

        using (var zos = new ZipOutputStream(ms) { IsStreamOwner = false })
        {
            var source = new StringMemoryDataSource("foo");
            zos.PutNextEntry(new ZipEntry(fileName) { HostSystem = (int)HostSystemID.Unix });
            source.GetSource().CopyTo(zos);
            zos.CloseEntry();
            zos.Finish();
        }

        ms.Seek(0, SeekOrigin.Begin);

        using (var zis = new ZipFile(ms))
        {
            var ze = zis.GetEntry(fileName);
            Assert.NotNull(ze);

            Assert.Equal((int)HostSystemID.Unix, ze.HostSystem);
            Assert.Equal(ZipConstants.VersionMadeBy, ze.VersionMadeBy);
        }
    }

    [Fact]
    [Trait("Category", "Zip")]
    public void HostSystemPersistedFromZipFile()
    {
        using var ms = new MemoryStream();
        var fileName = "testfile";

        using (var zof = new ZipFile(ms, true))
        {
            var ze = zof.EntryFactory.MakeFileEntry(fileName, false);
            ze.HostSystem = (int)HostSystemID.Unix;

            zof.BeginUpdate();
            zof.Add(new StringMemoryDataSource("foo"), ze);
            zof.CommitUpdate();
        }

        ms.Seek(0, SeekOrigin.Begin);

        using (var zis = new ZipFile(ms))
        {
            var ze = zis.GetEntry(fileName);
            Assert.NotNull(ze);

            Assert.Equal((int)HostSystemID.Unix, ze.HostSystem);
            Assert.Equal(ZipConstants.VersionMadeBy, ze.VersionMadeBy);
        }
    }

    /// <summary>
    /// Refs https://github.com/icsharpcode/SharpZipLib/issues/385
    /// Trying to add an AES Encrypted entry to ZipFile should throw as it isn't supported
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void AddingAnAESEncryptedEntryShouldThrow()
    {
        var memStream = new MemoryStream();
        using var zof = new ZipFile(memStream);
        var entry = new ZipEntry("test")
        {
            AESKeySize = 256,
        };

        zof.BeginUpdate();
        var exception = Assert.Throws<NotSupportedException>(() => zof.Add(new StringMemoryDataSource("foo"), entry));
        Assert.Equal("Creation of AES encrypted entries is not supported", exception?.Message);
    }

    /// <summary>
    /// Test that we can add a file entry and set the name to sometihng other than the name of the file.
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    [Trait("Category", "CreatesTempFile")]
    public void AddFileWithAlternateName()
    {
        // Create a unique name that will be different from the file name
        var fileName = Utils.GetDummyFileName();

        using var sourceFile = Utils.GetDummyFile(size: 16);
        using var outputFile = Utils.GetTempFile();
        var inputContent = File.ReadAllText(sourceFile);
        using (var zf = ZipFile.Create(outputFile))
        {
            zf.BeginUpdate();

            // Add a file with the unique display name
            zf.Add(sourceFile, fileName);
					
            zf.CommitUpdate();
            zf.Close();
        }

        using (var zipFile = new ZipFile(outputFile))
        {
            Assert.Equal(1, zipFile.Count);

            var fileEntry = zipFile.GetEntry(fileName);
            Assert.NotNull(fileEntry);

            using (var sr = new StreamReader(zipFile.GetInputStream(fileEntry)))
            {
                var outputContent = sr.ReadToEnd();
                Assert.Equal(inputContent, outputContent);
            }
        }
    }

    /// <summary>
    /// Test a zip file using BZip2 compression.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "Zip")]
    public void ZipWithBZip2Compression(bool encryptEntries)
    {
        var password = "pwd";

        using var memStream = new MemoryStream();
        using (var f = new ZipFile(memStream, leaveOpen: true))
        {
            if (encryptEntries)
            {
                f.Password = password;
            }

            f.BeginUpdate(new MemoryArchiveStorage());

            var m = new StringMemoryDataSource("BZip2Compressed");
            f.Add(m, "a.dat", CompressionMethod.BZip2);

            var m2 = new StringMemoryDataSource("DeflateCompressed");
            f.Add(m2, "b.dat", CompressionMethod.Deflated);
            f.CommitUpdate();
            ZipTesting.AssertPassesTestArchive(f);
        }

        memStream.Seek(0, SeekOrigin.Begin);

        using (var f = new ZipFile(memStream))
        {
            if (encryptEntries)
            {
                f.Password = password;
            }

            {
                var entry = f.GetEntry("a.dat");
                Assert.Equal(CompressionMethod.BZip2, entry.CompressionMethod);
                Assert.Equal(ZipConstants.VersionBZip2, entry.Version);
                Assert.Equal(encryptEntries, entry.IsCrypted);

                using var reader = new StreamReader(f.GetInputStream(entry));
                var contents = reader.ReadToEnd();
                Assert.Equal("BZip2Compressed", contents);
            }

            {
                var entry = f.GetEntry("b.dat");
                Assert.Equal(CompressionMethod.Deflated, entry.CompressionMethod);
                Assert.Equal(encryptEntries, entry.IsCrypted);

                using var reader = new StreamReader(f.GetInputStream(entry));
                var contents = reader.ReadToEnd();
                Assert.Equal("DeflateCompressed", contents);
            }
        }

        // @@TODO@@ verify the archive with 7-zip?
    }

    /// <summary>
    /// We should be able to read a bzip2 compressed zip file created by 7-zip.
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void ShouldReadBZip2ZipCreatedBy7Zip()
    {
        const string bZip2CompressedZipCreatedBy7Zip =
            "UEsDBC4AAAAMANu9SVxfPTiBlgAAAJwAAAAJAAAASGVsbG8udHh0QlpoOTFBWSZTWXpI+hEAAA6f" +
            "gEAFUAA6KEgQP+feYCAAahpMpomxqHopptTI9Tag0U8kxDQABoeoiyfRRN5ojfOiqtrxG2N2YKYn" +
            "WnGPFTA7ayWcNApshuy2iblQmSEHlS2gCYPi7WSiRFXyF8vNggFnXaHehK+TyucoQgu8qEhbF4GZ" +
            "pF83DDNIO/i7kinChIPSR9CIUEsBAi4ALgAAAAwA271JXF89OIGWAAAAnAAAAAkAAAAAAAAAAAAAAIAB" +
            "AAAAAEhlbGxvLnR4dFBLBQYAAAAAAQABADcAAAC9AAAAAAA=";

        const string originalText =
            "CodeBrix.Compression is a compression library that supports Zip files using both stored and deflate compression methods, PKZIP 2.0 style and AES encryption.";

        var fileBytes = Convert.FromBase64String(bZip2CompressedZipCreatedBy7Zip);

        using var input = new MemoryStream(fileBytes, writable: false);
        using var zf = new ZipFile(input);
        var entry = zf.GetEntry("Hello.txt");
        Assert.Equal(CompressionMethod.BZip2, entry.CompressionMethod);
        Assert.Equal(ZipConstants.VersionBZip2, entry.Version);

        using var reader = new StreamReader(zf.GetInputStream(entry));
        var contents = reader.ReadToEnd();
        Assert.Equal(originalText, contents);
    }

    /// <summary>
    /// We should be able to read a bzip2 compressed / AES encrypted zip file created by 7-zip.
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void ShouldReadAESBZip2ZipCreatedBy7Zip()
    {
        const string bZip2CompressedZipCreatedBy7Zip =
            "UEsDBC4AAQBjANu9SVxfPTiBsgAAAJwAAAAJAAsASGVsbG8udHh0AZkHAAEAQUUDDABNOz/lPhnt" +
            "q4Oruxv5ks/xu6+e71sojVPl2rq53nKAWxJ3sO2jKCmLjpSxxaBRGbRb8D86Kq+M/E29DcTUkgNr" +
            "sfcJVHdKyKjRJFVzQ1al0ze6TMiKPi+cxWxo/wlODU+me0bTTa8UyIR6qpezIixhpXTgEtghaQ7i" +
            "d7OnQzG4wXR2TYZ1gRzdA+XKsJ8imtKD+jIoxZ+P6YIBLduq69m/jL19zPrcs2KPAZb/AXcdcXPX" +
            "UEsBAi4ALgABAGMA271JXF89OIGyAAAAnAAAAAkACwAAAAAAAAAAAIABAAAAAEhlbGxvLnR4dAGZBwAB" +
            "AEFFAwwAUEsFBgAAAAABAAEAQgAAAOQAAAAAAA==";

        const string originalText =
            "CodeBrix.Compression is a compression library that supports Zip files using both stored and deflate compression methods, PKZIP 2.0 style and AES encryption.";

        var fileBytes = Convert.FromBase64String(bZip2CompressedZipCreatedBy7Zip);

        using var input = new MemoryStream(fileBytes, writable: false);
        using var zf = new ZipFile(input);
        zf.Password = "password";

        var entry = zf.GetEntry("Hello.txt");
        Assert.Equal(CompressionMethod.BZip2, entry.CompressionMethod);
        Assert.Equal(ZipConstants.VERSION_AES, entry.Version);
        Assert.True(entry.IsCrypted);
        Assert.Equal(256, entry.AESKeySize);

        using var reader = new StreamReader(zf.GetInputStream(entry));
        var contents = reader.ReadToEnd();
        Assert.Equal(originalText, contents);
    }

    /// <summary>
    /// Test for https://github.com/icsharpcode/SharpZipLib/issues/147, when deleting items in a zip
    /// </summary>
    /// <param name="useZip64">Whether Zip64 should be used in the test archive</param>
    [Theory]
    [InlineData(UseZip64.On)]
    [InlineData(UseZip64.Off)]
    [Trait("Category", "Zip")]
    public void TestDescriptorUpdateOnDelete(UseZip64 useZip64)
    {
        MemoryStream msw = new MemoryStreamWithoutSeek();
        using (var outStream = new ZipOutputStream(msw))
        {
            outStream.UseZip64 = useZip64;
            outStream.IsStreamOwner = false;
            outStream.PutNextEntry(new ZipEntry("StripedMarlin"));
            outStream.WriteByte(89);

            outStream.PutNextEntry(new ZipEntry("StripedMarlin2"));
            outStream.WriteByte(91);
        }

        var zipData = msw.ToArray();
        ZipTesting.AssertPassesTestArchive(zipData);

        using (var memoryStream = new MemoryStream(zipData))
        {
            using (var zipFile = new ZipFile(memoryStream, leaveOpen: true))
            {
                zipFile.BeginUpdate();
                zipFile.Delete("StripedMarlin");
                zipFile.CommitUpdate();
            }

            memoryStream.Position = 0;

            using (var zipFile = new ZipFile(memoryStream, leaveOpen: true))
            {
                ZipTesting.AssertPassesTestArchive(zipFile);
            }
        }
    }

    /// <summary>
    /// Test for https://github.com/icsharpcode/SharpZipLib/issues/147, when adding items to a zip
    /// </summary>
    /// <param name="useZip64">Whether Zip64 should be used in the test archive</param>
    [Theory]
    [InlineData(UseZip64.On)]
    [InlineData(UseZip64.Off)]
    [Trait("Category", "Zip")]
    public void TestDescriptorUpdateOnAdd(UseZip64 useZip64)
    {
        MemoryStream msw = new MemoryStreamWithoutSeek();
        using (var outStream = new ZipOutputStream(msw))
        {
            outStream.UseZip64 = useZip64;
            outStream.IsStreamOwner = false;
            outStream.PutNextEntry(new ZipEntry("StripedMarlin"));
            outStream.WriteByte(89);
        }

        var zipData = msw.ToArray();
        ZipTesting.AssertPassesTestArchive(zipData);

        using (var memoryStream = new MemoryStream())
        {
            memoryStream.Write(zipData, 0, zipData.Length);

            using (var zipFile = new ZipFile(memoryStream, leaveOpen: true))
            {
                zipFile.BeginUpdate();
                zipFile.Add(new StringMemoryDataSource("stripey"), "Zebra");
                zipFile.CommitUpdate();
            }

            memoryStream.Position = 0;

            using (var zipFile = new ZipFile(memoryStream, leaveOpen: true))
            {
                ZipTesting.AssertPassesTestArchive(zipFile);
            }
        }
    }

    /// <summary>
    /// Check that Zip files can be created with an empty file name
    /// </summary>
    [Fact]
    [Trait("Category", "Zip")]
    public void HandlesEmptyFileName()
    {
        using var ms = new MemoryStream();
        using (var zos = new ZipOutputStream(ms){IsStreamOwner = false})
        {
            zos.PutNextEntry(new ZipEntry(String.Empty));
            Utils.WriteDummyData(zos, 64);
        }
        ms.Seek(0, SeekOrigin.Begin);
        using (var zis = new ZipInputStream(ms){IsStreamOwner = false})
        {
            var entry = zis.GetNextEntry();
            Assert.Empty(entry.Name);
            Assert.Equal(64, zis.ReadBytes(64).Length);
        }
    }
}