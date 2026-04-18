using CodeBrix.Compression.Tests.TestSupport;
using CodeBrix.Compression.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using TimeSetting = CodeBrix.Compression.Zip.ZipEntryFactory.TimeSetting;

#pragma warning disable CS0618 // Type or member is obsolete

namespace CodeBrix.Compression.Tests.Zip;

[Trait("Category", "Zip")]
public class FastZipHandling : ZipBase
{
    [Fact]
    [Trait("Category", "CreatesTempFile")]
    public void Basics()
    {
        const string tempName1 = "a(1).dat";

        var target = new MemoryStream();

        var tempFilePath = GetTempFilePath();
        Assert.NotNull(tempFilePath);

        var addFile = Path.Combine(tempFilePath, tempName1);
        MakeTempFile(addFile, 1);

        try
        {
            var fastZip = new FastZip();
            fastZip.CreateZip(target, tempFilePath, false, @"a\(1\)\.dat", null);

            var archive = new MemoryStream(target.ToArray());
            using var zf = new ZipFile(archive);
            Assert.Equal(1, zf.Count);
            var entry = zf[0];
            Assert.Equal(tempName1, entry.Name);
            Assert.Equal(1, entry.Size);
            ZipTesting.AssertPassesTestArchive(zf);

            zf.Close();
        }
        finally
        {
            File.Delete(tempName1);
        }
    }

    private const string ZipTempDir = "CodeBrixCompressionTest";

    private void EnsureTestDirectoryIsEmpty(string baseDir)
    {
        var name = Path.Combine(baseDir, ZipTempDir);

        if (Directory.Exists(name))
        {
            Directory.Delete(name, true);
        }

        Directory.CreateDirectory(name);
    }

    [Fact]
    [Trait("Category", "CreatesTempFile")]
    public void ExtractEmptyDirectories()
    {
        var tempFilePath = GetTempFilePath();
        Assert.NotNull(tempFilePath);

        var name = Path.Combine(tempFilePath, "x.zip");

        EnsureTestDirectoryIsEmpty(tempFilePath);

        var targetDir = Path.Combine(tempFilePath, ZipTempDir + @"\floyd");
        using (var fs = File.Create(name))
        {
            using (var zOut = new ZipOutputStream(fs))
            {
                zOut.PutNextEntry(new ZipEntry("floyd/"));
            }
        }

        var fastZip = new FastZip();
        fastZip.CreateEmptyDirectories = true;
        fastZip.ExtractZip(name, targetDir, "zz");

        File.Delete(name);
        Assert.True(Directory.Exists(targetDir), "Empty directory should be created");
    }

    /// <summary>
    /// Test that FastZip can create empty directory entries in archives.
    /// </summary>
    [Theory]
    [Trait("Category", "CreatesTempFile")]
    [InlineData(null, null, false, false)]
    [InlineData("password", ZipEncryptionMethod.AES128, false, false)]
    [InlineData("password", ZipEncryptionMethod.ZipCrypto, true, false)]
    [InlineData("password", ZipEncryptionMethod.ZipCrypto, false, true)]
    public void CreateEmptyDirectories(string password, ZipEncryptionMethod? encryptionMethod, bool useIsUnderTest, bool shouldFail)
    {
        Exception thrown = null;

        try
        {
            using var tempFilePath = Utils.GetTempDir();
            var name = Path.Combine(tempFilePath.FullName, "x.zip");

            // Create empty test folders (The folder that we'll zip, and the test sub folder).
            var archiveRootDir = Path.Combine(tempFilePath.FullName, ZipTempDir);
            var targetDir = Path.Combine(archiveRootDir, "floyd");
            Directory.CreateDirectory(targetDir);

            // Create the archive with FastZip
            var fastZip = new FastZip
            {
                CreateEmptyDirectories = true,
                Password = password,
                EntryEncryptionMethod = encryptionMethod ?? ZipEncryptionMethod.None
            };

            if (useIsUnderTest)
            {
                fastZip.IsUnderTest = true;
            }

            fastZip.CreateZip(name, archiveRootDir, recurse: true, fileFilter: null);

            // Test that the archive contains the empty folder entry
            using var zipFile = new ZipFile(name);
            Assert.Equal(1, zipFile.Count);

            var folderEntry = zipFile.GetEntry("floyd/");
            Assert.True(folderEntry.IsDirectory, "The entry must be a folder");

            ZipTesting.AssertPassesTestArchive(zipFile);
        }
        catch (Exception e)
        {
            thrown = e;
        }

        if (shouldFail)
        {
            Assert.NotNull(thrown);
        }
        else
        {
            Assert.True(thrown == null, $"Did not expect an exception to be thrown, but got: {thrown}");
        }
    }

    [Theory]
    [Trait("Category", "CreatesTempFile")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    public void ContentEqualAfterAfterArchived(int contentSize)
    {
        using var sourceDir = Utils.GetTempDir();
        using var targetDir = Utils.GetTempDir();
        using var zipFile = Utils.GetTempFile();

        var sourceFile = sourceDir.CreateDummyFile(contentSize);
        var sourceContent = sourceFile.ReadAllBytes();
        new FastZip().CreateZip(zipFile.FullName, sourceDir.FullName, recurse: true, fileFilter: null);

        var ex = Record.Exception(() =>
        {
            new FastZip().ExtractZip(zipFile, targetDir, fileFilter: null);
        });
        Assert.True(ex == null, $"Exception during extraction of test archive: {ex}");

        var targetFile = Path.Combine(targetDir, Path.GetFileName(sourceFile));
        var targetContent = File.ReadAllBytes(targetFile);

        Assert.Equal(sourceContent.Length, targetContent.Length);
        Assert.Equal(sourceContent, targetContent);
    }

    [Theory]
    [InlineData(ZipEncryptionMethod.ZipCrypto, true, false)]
    [InlineData(ZipEncryptionMethod.ZipCrypto, false, true)]
    [InlineData(ZipEncryptionMethod.AES128, false, false)]
    [InlineData(ZipEncryptionMethod.AES256, false, false)]
    public void Encryption(ZipEncryptionMethod encryptionMethod, bool useIsUnderTest, bool shouldFail)
    {
        Exception thrown = null;
        const string tempName1 = "a.dat";
        const int tempSize = 1;

        var target = new MemoryStream();

        var tempFilePath = GetTempFilePath();
        Assert.NotNull(tempFilePath);

        var addFile = Path.Combine(tempFilePath, tempName1);
        MakeTempFile(addFile, tempSize);

        try
        {
            var fastZip = new FastZip
            {
                Password = "Ahoy",
                EntryEncryptionMethod = encryptionMethod
            };

            if (useIsUnderTest)
            {
                fastZip.IsUnderTest = true;
            }

            fastZip.CreateZip(target, tempFilePath, false, @"a\.dat", null);

            var archive = new MemoryStream(target.ToArray());
            using var zf = new ZipFile(archive);
            zf.Password = "Ahoy";
            Assert.Equal(1, zf.Count);
            var entry = zf[0];
            Assert.Equal(tempName1, entry.Name);
            Assert.Equal(tempSize, entry.Size);
            Assert.True(entry.IsCrypted);

            ZipTesting.AssertPassesTestArchive(zf);

            switch (encryptionMethod)
            {
                case ZipEncryptionMethod.ZipCrypto:
                    Assert.Equal(0, entry.AESKeySize);
                    break;

                case ZipEncryptionMethod.AES128:
                    Assert.Equal(128, entry.AESKeySize);
                    break;

                case ZipEncryptionMethod.AES256:
                    Assert.Equal(256, entry.AESKeySize);
                    break;
            }
        }
        catch (Exception ex)
        {
            thrown = ex;
        }
        finally
        {
            File.Delete(tempName1);
        }

        if (shouldFail)
        {
            Assert.NotNull(thrown);
        }
        else
        {
            Assert.True(thrown == null, $"Did not expect an exception to be thrown, but got: {thrown}");
        }
    }

    [Fact]
    public void CreateExceptions()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
        {
            using var tempDir = Utils.GetTempDir();
            var fastZip = new FastZip();
            var badPath = Path.Combine(Path.GetTempPath(), Utils.GetDummyFileName());
            var addFile = tempDir.GetFile("test.zip");
            fastZip.CreateZip(addFile, badPath, recurse: false, fileFilter: null);
        });
    }

    #region String testing helper

    private void TestFileNames(int codePage, IReadOnlyList<string> names)
    {
        var zippy = new FastZip();
        if (codePage > 0)
        {
            zippy.UseUnicode = false;
            zippy.LegacyCodePage = codePage;
        }

        using var tempDir = Utils.GetTempDir();
        using var tempZip = Utils.GetTempFile();
        var nameCount = 0;
        foreach (var name in names)
        {
            tempDir.CreateDummyFile(name);
            nameCount++;
        }

        zippy.CreateZip(tempZip, tempDir, recurse: true, fileFilter: null);

        using var zf = new ZipFile(tempZip, zippy.StringCodec);
        Assert.Equal(nameCount, zf.Count);
        foreach (var name in names)
        {
            var index = zf.FindEntry(name, ignoreCase: true);

            Assert.NotEqual(-1, index);

            var entry = zf[index];

            if (zippy.UseUnicode)
            {
                Assert.True(entry.IsUnicodeText, $"Zip entry #{index} not marked as unicode");
            }
            else
            {
                Assert.False(entry.IsUnicodeText, $"Zip entry #{index} marked as unicode");
            }

            Assert.Equal(name, entry.Name);

            var nameBytes = string.Join(" ", Encoding.BigEndianUnicode.GetBytes(entry.Name).Select(b => b.ToString("x2")));

            Console.WriteLine($" - Zip entry: {entry.Name} ({nameBytes})");
        }
    }

    #endregion String testing helper

    [Fact]
    [Trait("Category", "Unicode")]
    public void UnicodeText()
    {
        TestFileNames(0, StringTesting.Filenames.ToArray());
    }

    [Fact]
    [Trait("Category", "Unicode")]
    public void NonUnicodeText()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        foreach (var (language, filename, encoding) in StringTesting.TestSamples)
        {
            Console.WriteLine($"{language} filename \"{filename}\" using \"{encoding}\":");

            // TODO: samples of this test must be reversible
            // Some samples can't be restored back with their encoding.
            // test wasn't failing only because SystemDefaultCodepage is 65001 on Net.Core and
            // old behaviour actually was using Unicode instead of user's passed codepage
            var encoder = Encoding.GetEncoding(encoding);
            var bytes = encoder.GetBytes(filename);
            var restoredString = encoder.GetString(bytes);
            if(string.CompareOrdinal(filename, restoredString) != 0)
            {
                Console.WriteLine($"Sample for language {language} with value of {filename} is skipped, because it's irreversable");
                continue;
            }

            TestFileNames(Encoding.GetEncoding(encoding).CodePage, new [] { filename });
        }
    }

    [Fact]
    public void ExtractExceptions()
    {
        var fastZip = new FastZip();
        var tempFilePath = GetTempFilePath();
        Assert.NotNull(tempFilePath);

        var addFile = Path.Combine(tempFilePath, "test.zip");
        try
        {
            Assert.Throws<FileNotFoundException>(() => fastZip.ExtractZip(addFile, @"z:\doesnt exist", null));
        }
        finally
        {
            File.Delete(addFile);
        }
    }

    [Fact(Skip = "see comments below")]
    /*
     * This test is somewhat strange:
     * a) It tries to simulate a locked file by opening it on the same thread using FileShare.
     *    However the FileShare value is not meant for cross-process file locking, but only to
     *    allow other threads in the same process to access the same file.
     *    This is not the intended behavior, you would need a second process locking the file
     *    when running this test.
     * b) It would require to change the file operation in FastZip.ProcessFile to use FileShare.ReadWrite
     *    but doing so would make FastZip work with locked files (that are potentially written to by others)
     *    and silently ignoring any locks. HOWEVER: This can lead to corrupt/incomplete files, which is why it
     *    should not be the default behavior.
     *
     * Therefore I would remove this test.
     **/
    public void ReadingOfLockedDataFiles()
    {
        const string tempName1 = "a.dat";
        const int tempSize = 1;

        var target = new MemoryStream();

        var tempFilePath = GetTempFilePath();
        Assert.NotNull(tempFilePath);

        var addFile = Path.Combine(tempFilePath, tempName1);
        MakeTempFile(addFile, tempSize);

        try
        {
            var fastZip = new FastZip();

            using (File.Open(addFile, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                fastZip.CreateZip(target, tempFilePath, false, @"a\.dat", null);

                var archive = new MemoryStream(target.ToArray());
                using (var zf = new ZipFile(archive))
                {
                    Assert.Equal(1, zf.Count);
                    var entry = zf[0];
                    Assert.Equal(tempName1, entry.Name);
                    Assert.Equal(tempSize, entry.Size);
                    ZipTesting.AssertPassesTestArchive(zf);

                    zf.Close();
                }
            }
        }
        finally
        {
            File.Delete(tempName1);
        }
    }

    [Theory]
    [InlineData(ZipEncryptionMethod.ZipCrypto, true, false)]
    [InlineData(ZipEncryptionMethod.ZipCrypto, false, true)]
    [InlineData(ZipEncryptionMethod.AES128, false, false)]
    public void NonAsciiPasswords(ZipEncryptionMethod encryptionMethod, bool useIsUnderTest, bool shouldFail)
    {
        Exception thrown = null;

        const string tempName1 = "a.dat";
        const int tempSize = 1;

        var target = new MemoryStream();

        var tempFilePath = GetTempFilePath();
        Assert.NotNull(tempFilePath);

        var addFile = Path.Combine(tempFilePath, tempName1);
        MakeTempFile(addFile, tempSize);

        var password = "abc\u0066\u0393";
        try
        {
            var fastZip = new FastZip()
            {
                EntryEncryptionMethod = encryptionMethod,
            };

            if (useIsUnderTest)
            {
                fastZip.IsUnderTest = true;
            }

            fastZip.Password = password;

            fastZip.CreateZip(target, tempFilePath, false, @"a\.dat", null);

            var archive = new MemoryStream(target.ToArray());
            using var zf = new ZipFile(archive);
            zf.Password = password;
            Assert.Equal(1, zf.Count);
            var entry = zf[0];
            Assert.Equal(tempName1, entry.Name);
            Assert.Equal(tempSize, entry.Size);
            ZipTesting.AssertPassesTestArchive(zf);
            Assert.True(entry.IsCrypted);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }
        finally
        {
            File.Delete(tempName1);
        }

        if (shouldFail)
        {
            Assert.NotNull(thrown);
        }
        else
        {
            Assert.True(thrown == null, $"Did not expect an exception to be thrown, but got: {thrown}");
        }
    }

    [Fact]
    [Trait("Category", "CreatesTempFile")]
    public void LimitExtractPath()
    {
        var tempPath = GetTempFilePath();
        Assert.NotNull(tempPath);

        var uniqueName = "CodeBrix.Compression.Test_" + DateTime.Now.Ticks.ToString("x");

        tempPath = Path.Combine(tempPath, uniqueName);
        var extractPath = Path.Combine(tempPath, "output");

        const string contentFile = "output.txt";

        var contentFilePathBad = Path.Combine("..", contentFile);
        var extractFilePathBad = Path.Combine(tempPath, contentFile);
        var archiveFileBad = Path.Combine(tempPath, "test-good.zip");

        var contentFilePathGood = Path.Combine("childDir", contentFile);
        var extractFilePathGood = Path.Combine(extractPath, contentFilePathGood);
        var archiveFileGood = Path.Combine(tempPath, "test-bad.zip");

        try
        {
            Directory.CreateDirectory(extractPath);

            // Create test input
            void CreateTestFile(string archiveFile, string contentPath)
            {
                using var zf = ZipFile.Create(archiveFile);
                zf.BeginUpdate();
                zf.Add(new StringMemoryDataSource($"Content of {archiveFile}"), contentPath);
                zf.CommitUpdate();
            }

            CreateTestFile(archiveFileGood, contentFilePathGood);
            CreateTestFile(archiveFileBad, contentFilePathBad);

            Assert.True(File.Exists(archiveFileGood), "Good test archive was not created");
            Assert.True(File.Exists(archiveFileBad), "Bad test archive was not created");

            var fastZip = new FastZip();

            var ex1 = Record.Exception(() =>
            {
                fastZip.ExtractZip(archiveFileGood, extractPath, "");
            });
            Assert.True(ex1 == null, $"Threw exception on good file name: {ex1}");

            Assert.True(File.Exists(extractFilePathGood), "Good output file not created");

            Assert.Throws<CodeBrix.Compression.Core.InvalidNameException>(() =>
            {
                fastZip.ExtractZip(archiveFileBad, extractPath, "");
            });

            Assert.False(File.Exists(extractFilePathBad), "Bad output file created");

            var ex2 = Record.Exception(() =>
            {
                fastZip.ExtractZip(archiveFileBad, extractPath, FastZip.Overwrite.Never, null, "", "", true, true);
            });
            Assert.True(ex2 == null, $"Threw exception on bad file name when traversal explicitly allowed: {ex2}");

            Assert.True(File.Exists(extractFilePathBad), "Bad output file not created when traversal explicitly allowed");
        }
        finally
        {
            Directory.Delete(tempPath, true);
        }
    }

    /// <summary>
    /// Check that the input stream is not closed on error when isStreamOwner is false
    /// </summary>
    [Fact]
    public void StreamNotClosedOnError()
    {
        // test paths
        var tempFilePath = GetTempFilePath();
        Assert.NotNull(tempFilePath);

        var tempFolderPath = Path.Combine(tempFilePath, Path.GetRandomFileName());
        Assert.False(Directory.Exists(tempFolderPath), "Temp folder path should not exist");

        // memory that isn't a valid zip
        var ms = new TrackedMemoryStream(new byte[32]);
        Assert.False(ms.IsClosed, "Underlying stream should NOT be closed initially");

        // Try to extract
        var fastZip = new FastZip();
        fastZip.CreateEmptyDirectories = true;

        Assert.Throws<ZipException>(() => fastZip.ExtractZip(ms, tempFolderPath, FastZip.Overwrite.Always, null, "a", "b", false, false));
        Assert.False(ms.IsClosed, "inputStream stream should NOT be closed when isStreamOwner is false");

        // test folder should not have been created on error
        Assert.False(Directory.Exists(tempFolderPath), "Temp folder path should still not exist");
    }

    /// <summary>
    /// Check that the input stream is closed on error when isStreamOwner is true
    /// </summary>
    [Fact]
    public void StreamClosedOnError()
    {
        // test paths
        var tempFilePath = GetTempFilePath();
        Assert.NotNull(tempFilePath);

        var tempFolderPath = Path.Combine(tempFilePath, Path.GetRandomFileName());
        Assert.False(Directory.Exists(tempFolderPath), "Temp folder path should not exist");

        // memory that isn't a valid zip
        var ms = new TrackedMemoryStream(new byte[32]);
        Assert.False(ms.IsClosed, "Underlying stream should NOT be closed initially");

        // Try to extract
        var fastZip = new FastZip();
        fastZip.CreateEmptyDirectories = true;

        Assert.Throws<ZipException>(() => fastZip.ExtractZip(ms, tempFolderPath, FastZip.Overwrite.Always, null, "a", "b", false, true));
        Assert.True(ms.IsClosed, "inputStream stream should be closed when isStreamOwner is true");

        // test folder should not have been created on error
        Assert.False(Directory.Exists(tempFolderPath), "Temp folder path should still not exist");
    }

    /// <summary>
    /// #426 - set the modified date for created directory entries if the RestoreDateTimeOnExtract option is enabled
    /// </summary>
    [Fact]
    [Trait("Category", "CreatesTempFile")]
    public void SetDirectoryModifiedDate()
    {
        var tempFilePath = GetTempFilePath();
        Assert.NotNull(tempFilePath);

        var zipName = Path.Combine(tempFilePath, $"{nameof(SetDirectoryModifiedDate)}.zip");

        EnsureTestDirectoryIsEmpty(tempFilePath);

        var modifiedTime = new DateTime(2001, 1, 2);
        var targetDir = Path.Combine(tempFilePath, ZipTempDir, nameof(SetDirectoryModifiedDate));
        using (var fs = File.Create(zipName))
        {
            using (var zOut = new ZipOutputStream(fs))
            {
                // Add an empty directory entry, with a specified time field
                var entry = new ZipEntry("emptyFolder/")
                {
                    DateTime = modifiedTime
                };
                zOut.PutNextEntry(entry);
            }
        }

        try
        {
            // extract the zip
            var fastZip = new FastZip
            {
                CreateEmptyDirectories = true,
                RestoreDateTimeOnExtract = true
            };
            fastZip.ExtractZip(zipName, targetDir, "zz");

            File.Delete(zipName);

            // Check that the empty sub folder exists and has the expected modlfied date
            var emptyTargetDir = Path.Combine(targetDir, "emptyFolder");

            Assert.True(Directory.Exists(emptyTargetDir), "Empty directory should be created");

            var extractedFolderTime = Directory.GetLastWriteTime(emptyTargetDir);
            Assert.Equal(modifiedTime, extractedFolderTime);
        }
        finally
        {
            // Tidy up
            Directory.Delete(targetDir, true);
        }
    }

    /// <summary>
    /// Test for https://github.com/icsharpcode/SharpZipLib/issues/78
    /// </summary>
    /// <param name="leaveOpen">if true, the stream given to CreateZip should be left open, if false it should be disposed.</param>
    [Theory]
    [Trait("Category", "CreatesTempFile")]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateZipShouldLeaveOutputStreamOpenIfRequested(bool leaveOpen)
    {
        const string tempFileName = "a(2).dat";
        const int tempSize = 16;

        using var tempFolder = Utils.GetTempDir();
        // Create test input file
        tempFolder.CreateDummyFile(tempFileName, tempSize);

        // Create the zip with fast zip
        var target = new TrackedMemoryStream();
        var fastZip = new FastZip();

        fastZip.CreateZip(target, tempFolder, recurse: false, @"a\(2\)\.dat", directoryFilter: null, leaveOpen);

        // Check that the output stream was disposed (or not) as expected
        Assert.NotEqual(leaveOpen, target.IsDisposed);

        // Check that the file contents are correct in both cases
        var archive = new MemoryStream(target.ToArray());
        using var zf = new ZipFile(archive);
        Assert.Equal(1, zf.Count);
        var entry = zf[0];
        Assert.Equal(tempFileName, entry.Name);
        Assert.Equal(tempSize, entry.Size);
        ZipTesting.AssertPassesTestArchive(zf);
    }

    [Trait("Category", "CreatesTempFile")]
    [Fact]
    public void CreateZipShouldSetTimeOnEntriesFromConstructorDateTime()
    {
        var targetTime = TestTargetTime(TimeSetting.Fixed);
        var fastZip = new FastZip(targetTime);
        var target = CreateFastZipTestArchiveWithAnEntry(fastZip);
        var archive = new MemoryStream(target.ToArray());
        using var zf = new ZipFile(archive);
        Assert.Equal(targetTime, zf[0].DateTime);
    }

    [Theory]
    [Trait("Category", "CreatesTempFile")]
    [InlineData(TimeSetting.CreateTimeUtc)]
    [InlineData(TimeSetting.LastWriteTimeUtc)]
    [InlineData(TimeSetting.LastAccessTimeUtc)]
    [InlineData(TimeSetting.CreateTime)]
    [InlineData(TimeSetting.LastWriteTime)]
    [InlineData(TimeSetting.LastAccessTime)]
    public void CreateZipShouldSetTimeOnEntriesFromConstructorTimeSetting(TimeSetting timeSetting)
    {
        var targetTime = TestTargetTime(timeSetting);
        var fastZip = new FastZip(timeSetting);

        var alterTime = (Action<FileInfo>) null;
        switch(timeSetting)
        {
            case TimeSetting.LastWriteTime: alterTime = fi => fi.LastWriteTime = targetTime; break;
            case TimeSetting.LastWriteTimeUtc: alterTime = fi => fi.LastWriteTimeUtc = targetTime; break;
            case TimeSetting.CreateTime: alterTime =  fi => fi.CreationTime = targetTime; break;
            case TimeSetting.CreateTimeUtc: alterTime =  fi => fi.CreationTimeUtc = targetTime; break;
        }

        var target = CreateFastZipTestArchiveWithAnEntry(fastZip, alterTime);
        // Check that the file contents are correct in both cases
        var archive = new MemoryStream(target.ToArray());
        using var zf = new ZipFile(archive);
        var expectedTime = TestTargetTime(timeSetting);
        var actualTime = zf[0].DateTime;
        // Assert that the time is within +/- 2s of the target time to allow for timing/rounding discrepancies
        Assert.True(Math.Abs((expectedTime - actualTime).TotalSeconds) <= 2);
    }

    [Theory]
    [Trait("Category", "CreatesTempFile")]
    [InlineData(TimeSetting.CreateTimeUtc)]
    [InlineData(TimeSetting.LastWriteTimeUtc)]
    [InlineData(TimeSetting.LastAccessTimeUtc)]
    [InlineData(TimeSetting.CreateTime)]
    [InlineData(TimeSetting.LastWriteTime)]
    [InlineData(TimeSetting.LastAccessTime)]
    [InlineData(TimeSetting.Fixed)]
    public void ExtractZipShouldSetTimeOnFilesFromConstructorTimeSetting(TimeSetting timeSetting)
    {
        var targetTime = ExpectedFixedTime();
        var archiveStream = CreateFastZipTestArchiveWithAnEntry(new FastZip(targetTime));

        if (timeSetting == TimeSetting.Fixed)
        {
            Assert.Skip("Fixed time without specifying a time is undefined");
            return;
        }

        var fastZip = new FastZip(timeSetting);
        using var extractDir = Utils.GetTempDir();
        fastZip.ExtractZip(archiveStream, extractDir.FullName, FastZip.Overwrite.Always,
            _ => true, "", "", restoreDateTime: true, isStreamOwner: true, allowParentTraversal: false);
        var fi = new FileInfo(Path.Combine(extractDir.FullName, SingleEntryFileName));
        var actualTime = FileTimeFromTimeSetting(fi, timeSetting);
        // Assert that the time is within +/- 2s of the target time to allow for timing/rounding discrepancies
        Assert.True(Math.Abs((targetTime - actualTime).TotalSeconds) <= 2);
    }

    [Theory]
    [Trait("Category", "CreatesTempFile")]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void ExtractZipShouldSetTimeOnFilesFromConstructorDateTime(DateTimeKind dtk)
    {
        // Create the archive with a fixed "bad" datetime
        var target = CreateFastZipTestArchiveWithAnEntry(new FastZip(UnexpectedFixedTime(dtk)));

        // Extract the archive with a fixed time override
        var targetTime = ExpectedFixedTime(dtk);
        var fastZip = new FastZip(targetTime);
        using var extractDir = Utils.GetTempDir();
        fastZip.ExtractZip(target, extractDir.FullName, FastZip.Overwrite.Always,
            _ => true, "", "", restoreDateTime: true, isStreamOwner: true, allowParentTraversal: false);
        var fi = new FileInfo(Path.Combine(extractDir.FullName, SingleEntryFileName));
        var fileTime = FileTimeFromTimeSetting(fi, TimeSetting.Fixed);
        if (fileTime.Kind != dtk)
        {
            fileTime = fileTime.ToUniversalTime();
        }

        Assert.Equal(targetTime, fileTime);
    }

    [Theory]
    [Trait("Category", "CreatesTempFile")]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void ExtractZipShouldSetTimeOnFilesWithEmptyConstructor(DateTimeKind dtk)
    {
        // Create the archive with a fixed datetime
        var targetTime = ExpectedFixedTime(dtk);
        var target = CreateFastZipTestArchiveWithAnEntry(new FastZip(targetTime));

        // Extract the archive with an empty constructor
        var fastZip = new FastZip();
        using var extractDir = Utils.GetTempDir();
        fastZip.ExtractZip(target, extractDir.FullName, FastZip.Overwrite.Always,
            _ => true, "", "", restoreDateTime: true, isStreamOwner: true, allowParentTraversal: false);
        var fi = new FileInfo(Path.Combine(extractDir.FullName, SingleEntryFileName));
        Assert.Equal(targetTime, FileTimeFromTimeSetting(fi, TimeSetting.Fixed));
    }

    private static bool IsLastAccessTime(TimeSetting ts)
        => ts == TimeSetting.LastAccessTime || ts == TimeSetting.LastAccessTimeUtc;

    private static DateTime FileTimeFromTimeSetting(FileInfo fi, TimeSetting timeSetting)
    {
        switch (timeSetting)
        {
            case TimeSetting.LastWriteTime: return fi.LastWriteTime;
            case TimeSetting.LastWriteTimeUtc: return fi.LastWriteTimeUtc;
            case TimeSetting.CreateTime: return fi.CreationTime;
            case TimeSetting.CreateTimeUtc: return fi.CreationTimeUtc;
            case TimeSetting.LastAccessTime: return fi.LastAccessTime;
            case TimeSetting.LastAccessTimeUtc: return fi.LastAccessTimeUtc;
            case TimeSetting.Fixed: return fi.LastWriteTime;
        }

        throw new ArgumentException("Invalid TimeSetting", nameof(timeSetting));
    }

    private static DateTime TestTargetTime(TimeSetting ts)
    {
        var dtk = ts == TimeSetting.CreateTimeUtc
                  || ts == TimeSetting.LastWriteTimeUtc
                  || ts == TimeSetting.LastAccessTimeUtc
            ? DateTimeKind.Utc
            : DateTimeKind.Local;

        return IsLastAccessTime(ts)
            // AccessTime will be altered by reading/writing the file entry
            ? CurrentTime(dtk)
            : ExpectedFixedTime(dtk);
    }

    private static DateTime CurrentTime(DateTimeKind kind)
    {
        var now = kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;
        return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, (now.Second / 2) * 2, kind);
    }

    private static DateTime ExpectedFixedTime(DateTimeKind dtk = DateTimeKind.Unspecified) =>
        new(2010, 5, 30, 16, 22, 50, dtk);
    private static DateTime UnexpectedFixedTime(DateTimeKind dtk = DateTimeKind.Unspecified) =>
        new(1980, 10, 11, 22, 39, 30, dtk);

    private const string SingleEntryFileName = "testEntry.dat";

    private static TrackedMemoryStream CreateFastZipTestArchiveWithAnEntry(FastZip fastZip, Action<FileInfo> alterFile = null)
    {
        var target = new TrackedMemoryStream();

        using var tempFolder = Utils.GetTempDir();
        // Create test input file
        var addFile = Path.Combine(tempFolder.FullName, SingleEntryFileName);
        MakeTempFile(addFile, 16);
        var fi = new FileInfo(addFile);
        alterFile?.Invoke(fi);

        fastZip.CreateZip(target, tempFolder.FullName, recurse: false,
            SingleEntryFileName, directoryFilter: null, leaveOpen: true);

        return target;
    }
}
