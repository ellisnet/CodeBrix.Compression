using CodeBrix.Compression.Tests.TestSupport;
using CodeBrix.Compression.Zip;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Compression.Tests.Zip;

[Trait("Category", "Encryption")]
[Trait("Category", "Zip")]
public class ZipEncryptionHandling
{
    [Theory]
    [InlineData(CompressionMethod.Stored)]
    [InlineData(CompressionMethod.Deflated)]
    public void Aes128Encryption(CompressionMethod compressionMethod)
    {
        CreateZipWithEncryptedEntries("foo", 128, compressionMethod);
    }

    [Theory]
    [InlineData(CompressionMethod.Stored)]
    [InlineData(CompressionMethod.Deflated)]
    public void Aes256Encryption(CompressionMethod compressionMethod)
    {
        CreateZipWithEncryptedEntries("foo", 256, compressionMethod);
    }

    [Theory]
    [InlineData(CompressionMethod.Stored)]
    [InlineData(CompressionMethod.Deflated)]
    public void ZipCryptoEncryption(CompressionMethod compressionMethod)
    {
        CreateZipWithEncryptedEntries("foo", 0, compressionMethod);
    }

    /// <summary>
    /// Test Known zero length encrypted entries with ZipOutputStream.
    /// These are entries where the entry size is set to 0 ahead of time, so that PutNextEntry will fill in the header and there will be no patching.
    /// Test with Zip64 on and off, as the logic is different for the two.
    /// </summary>
    [Theory]
    [InlineData(UseZip64.Off, 0, CompressionMethod.Stored)]
    [InlineData(UseZip64.Off, 0, CompressionMethod.Deflated)]
    [InlineData(UseZip64.Off, 128, CompressionMethod.Stored)]
    [InlineData(UseZip64.Off, 128, CompressionMethod.Deflated)]
    [InlineData(UseZip64.Off, 256, CompressionMethod.Stored)]
    [InlineData(UseZip64.Off, 256, CompressionMethod.Deflated)]
    [InlineData(UseZip64.On, 0, CompressionMethod.Stored)]
    [InlineData(UseZip64.On, 0, CompressionMethod.Deflated)]
    [InlineData(UseZip64.On, 128, CompressionMethod.Stored)]
    [InlineData(UseZip64.On, 128, CompressionMethod.Deflated)]
    [InlineData(UseZip64.On, 256, CompressionMethod.Stored)]
    [InlineData(UseZip64.On, 256, CompressionMethod.Deflated)]
    [InlineData(UseZip64.Dynamic, 0, CompressionMethod.Stored)]
    [InlineData(UseZip64.Dynamic, 0, CompressionMethod.Deflated)]
    [InlineData(UseZip64.Dynamic, 128, CompressionMethod.Stored)]
    [InlineData(UseZip64.Dynamic, 128, CompressionMethod.Deflated)]
    [InlineData(UseZip64.Dynamic, 256, CompressionMethod.Stored)]
    [InlineData(UseZip64.Dynamic, 256, CompressionMethod.Deflated)]
    public void ZipOutputStreamEncryptEmptyEntries(
        UseZip64 useZip64,
        int keySize,
        CompressionMethod compressionMethod)
    {
        using var ms = new MemoryStream();
        using (var zipOutputStream = new ZipOutputStream(ms))
        {
            zipOutputStream.IsStreamOwner = false;
            zipOutputStream.Password = "password";
            zipOutputStream.UseZip64 = useZip64;

            var zipEntry = new ZipEntry("emptyEntry")
            {
                AESKeySize = keySize,
                CompressionMethod = compressionMethod,
                CompressedSize = 0,
                Crc = 0,
                Size = 0,
            };

            zipOutputStream.PutNextEntry(zipEntry);
            zipOutputStream.CloseEntry();
        }

        SevenZipHelper.VerifyZipWith7Zip(ms, "password");
    }

    [Fact]
    public void ZipFileAesDecryption()
    {
        var password = "password";

        using var ms = new MemoryStream();
        WriteEncryptedZipToStream(ms, password, 256);

        var zipFile = new ZipFile(ms)
        {
            Password = password
        };

        foreach (var entry in zipFile)
        {
            if (!entry.IsFile)
            {
                continue;
            }

            using var zis = zipFile.GetInputStream(entry);
            using var sr = new StreamReader(zis, Encoding.UTF8);
            var content = sr.ReadToEnd();
            Assert.Equal(DummyDataString, content);
        }

        ZipTesting.AssertPassesTestArchive(zipFile, testData: false);
    }

    [Fact]
    public void ZipFileAesRead()
    {
        var password = "password";

        using var ms = new SingleByteReadingStream();
        WriteEncryptedZipToStream(ms, password, 256);
        ms.Seek(0, SeekOrigin.Begin);

        var zipFile = new ZipFile(ms)
        {
            Password = password
        };

        foreach (var entry in zipFile)
        {
            if (!entry.IsFile)
            {
                continue;
            }

            using var zis = zipFile.GetInputStream(entry);
            using var sr = new StreamReader(zis, Encoding.UTF8);
            var content = sr.ReadToEnd();
            Assert.Equal(DummyDataString, content);
        }
    }

    /// <summary>
    /// Test using AES encryption on a file whose contents are Stored rather than deflated
    /// </summary>
    [Fact]
    public void ZipFileStoreAes()
    {
        var password = "password";

        // Make an encrypted zip file
        using var memoryStream = MakeAESEncryptedZipStream(password);
        // try to read it
        var zipFile = new ZipFile(memoryStream, leaveOpen: true)
        {
            Password = password
        };

        foreach (var entry in zipFile)
        {
            if (!entry.IsFile)
            {
                continue;
            }

            // Should be stored rather than deflated
            Assert.Equal(CompressionMethod.Stored, entry.CompressionMethod);

            using var zis = zipFile.GetInputStream(entry);
            using var sr = new StreamReader(zis, Encoding.UTF8);
            var content = sr.ReadToEnd();
            Assert.Equal(DummyDataString, content);
        }
    }

    /// <summary>
    /// As <see cref="ZipFileStoreAes"/>, but with Async reads
    /// </summary>
    [Fact]
    public async Task ZipFileStoreAesAsync()
    {
        var password = "password";

        // Make an encrypted zip file
        await using var memoryStream = MakeAESEncryptedZipStream(password);
        // try to read it
        var zipFile = new ZipFile(memoryStream, leaveOpen: true)
        {
            Password = password
        };

        foreach (var entry in zipFile)
        {
            // Should be stored rather than deflated
            Assert.Equal(CompressionMethod.Stored, entry.CompressionMethod);

            await using var zis = zipFile.GetInputStream(entry);
            await using var inputStream = zipFile.GetInputStream(entry);
            using var sr = new StreamReader(zis, Encoding.UTF8);
            var content = await sr.ReadToEndAsync(CancellationToken.None);
            Assert.Equal(DummyDataString, content);
        }
    }

    // Shared helper for the ZipFileStoreAes tests
    private static Stream MakeAESEncryptedZipStream(string password)
    {
        var memoryStream = new MemoryStream();

        // Try to create a zip stream
        WriteEncryptedZipToStream(memoryStream, password, 256, CompressionMethod.Stored);

        // reset
        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream;
    }

    /// <summary>
    /// Test using AES encryption on a file whose contents are Stored rather than deflated
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(17)]
    public void ZipFileStoreAesPartialRead(int readSize)
    {
        var password = "password";

        using var memoryStream = new MemoryStream();
        // Try to create a zip stream
        WriteEncryptedZipToStream(memoryStream, password, 256, CompressionMethod.Stored);

        // reset
        memoryStream.Seek(0, SeekOrigin.Begin);

        // try to read it
        var zipFile = new ZipFile(memoryStream, leaveOpen: true)
        {
            Password = password
        };

        foreach (var entry in zipFile)
        {
            if (!entry.IsFile)
            {
                continue;
            }

            // Should be stored rather than deflated
            Assert.Equal(CompressionMethod.Stored, entry.CompressionMethod);

            using var ms = new MemoryStream();
            using (var zis = zipFile.GetInputStream(entry))
            {
                var buffer = new byte[readSize];

                while (true)
                {
                    var read = zis.Read(buffer, 0, readSize);

                    if (read == 0)
                    {
                        break;
                    }

                    ms.Write(buffer, 0, read);
                }
            }

            ms.Seek(0, SeekOrigin.Begin);

            using (var sr = new StreamReader(ms, Encoding.UTF8))
            {
                var content = sr.ReadToEnd();
                Assert.Equal(DummyDataString, content);
            }
        }
    }

    /// <summary>
    /// Test adding files to an encrypted zip
    /// </summary>
    [Fact]
    public void ZipFileAesAdd()
    {
        var password = "password";
        var testData = "AdditionalData";
        var keySize = 256;

        using var memoryStream = new MemoryStream();
        // Try to create a zip stream
        WriteEncryptedZipToStream(memoryStream, password, keySize, CompressionMethod.Deflated);

        // reset
        memoryStream.Seek(0, SeekOrigin.Begin);

        // Update the archive with ZipFile
        {
            using var zipFile = new ZipFile(memoryStream, leaveOpen: true) { Password = password };
            zipFile.BeginUpdate();
            zipFile.Add(new StringMemoryDataSource(testData), "AdditionalEntry", CompressionMethod.Deflated);
            zipFile.CommitUpdate();
        }

        // Test the updated archive
        {
            memoryStream.Seek(0, SeekOrigin.Begin);

            using var zipFile = new ZipFile(memoryStream, leaveOpen: true) { Password = password };
            Assert.Equal(2, zipFile.Count);

            // Disabled because of bug #317
            // Assert.True(zipFile.TestArchive(true));

            // Check the original entry
            {
                var originalEntry = zipFile.GetEntry("test");
                Assert.True(originalEntry.IsCrypted);
                Assert.Equal(keySize, originalEntry.AESKeySize);


                using var zis = zipFile.GetInputStream(originalEntry);
                using var sr = new StreamReader(zis, Encoding.UTF8);
                var content = sr.ReadToEnd();
                Assert.Equal(DummyDataString, content);
            }

            // Check the additional entry
            // This should be encrypted, though currently only with ZipCrypto
            {
                var additionalEntry = zipFile.GetEntry("AdditionalEntry");
                Assert.True(additionalEntry.IsCrypted);

                using var zis = zipFile.GetInputStream(additionalEntry);
                using var sr = new StreamReader(zis, Encoding.UTF8);
                var content = sr.ReadToEnd();
                Assert.Equal(testData, content);
            }
        }

        // As an extra test, verify the file with 7-zip
        SevenZipHelper.VerifyZipWith7Zip(memoryStream, password);
    }

    /// <summary>
    /// Test deleting files from an encrypted zip
    /// </summary>
    [Fact]
    public void ZipFileAesDelete()
    {
        var password = "password";
        var keySize = 256;

        using var memoryStream = new MemoryStream();
        // Try to create a zip stream
        WriteEncryptedZipToStream(memoryStream, 3, password, keySize, CompressionMethod.Deflated);

        // reset
        memoryStream.Seek(0, SeekOrigin.Begin);

        // delete one of the entries from the file
        {
            using var zipFile = new ZipFile(memoryStream, leaveOpen: true) { Password = password };
            // Must have 3 entries to start with
            Assert.Equal(3, zipFile.Count);

            var entryToDelete = zipFile.GetEntry("test-1");
            Assert.NotNull(entryToDelete);

            zipFile.BeginUpdate();
            zipFile.Delete(entryToDelete);
            zipFile.CommitUpdate();
        }

        // Test the updated archive
        {
            memoryStream.Seek(0, SeekOrigin.Begin);

            using var zipFile = new ZipFile(memoryStream, leaveOpen: true) { Password = password };
            // We should now only have 2 files
            Assert.Equal(2, zipFile.Count);

            // Disabled because of bug #317
            // Assert.True(zipFile.TestArchive(true));

            // Check the first entry
            {
                var originalEntry = zipFile.GetEntry("test-0");
                Assert.True(originalEntry.IsCrypted);
                Assert.Equal(keySize, originalEntry.AESKeySize);


                using var zis = zipFile.GetInputStream(originalEntry);
                using var sr = new StreamReader(zis, Encoding.UTF8);
                var content = sr.ReadToEnd();
                Assert.Equal(DummyDataString, content);
            }

            // Check the second entry
            {
                var originalEntry = zipFile.GetEntry("test-2");
                Assert.True(originalEntry.IsCrypted);
                Assert.Equal(keySize, originalEntry.AESKeySize);


                using var zis = zipFile.GetInputStream(originalEntry);
                using var sr = new StreamReader(zis, Encoding.UTF8);
                var content = sr.ReadToEnd();
                Assert.Equal(DummyDataString, content);
            }
        }

        // As an extra test, verify the file with 7-zip
        SevenZipHelper.VerifyZipWith7Zip(memoryStream, password);
    }

    // This is a zip file with one AES encrypted entry, whose password in an empty string.
    const string TestFileWithEmptyPassword = @"UEsDBDMACQBjACaj0FAyKbop//////////8EAB8AdGVzdAEAEAA4AAAA
			AAAAAFIAAAAAAAAAAZkHAAIAQUUDCABADvo3YqmCtIE+lhw26kjbqkGsLEOk6bVA+FnSpVD4yGP4Mr66Hs14aTtsPUaANX2
            Z6qZczEmwoaNQpNBnKl7p9YOG8GSHDfTCUU/AZvT4yGFhUEsHCDIpuilSAAAAAAAAADgAAAAAAAAAUEsBAjMAMwAJAGMAJq
            PQUDIpuin//////////wQAHwAAAAAAAAAAAAAAAAAAAHRlc3QBABAAOAAAAAAAAABSAAAAAAAAAAGZBwACAEFFAwgAUEsFBgAAAAABAAEAUQAAAKsAAAAAAA==";

    /// <summary>
    /// Test reading an AES encrypted entry whose password is an empty string.
    /// </summary>
    /// <remarks>
    /// Test added for https://github.com/icsharpcode/SharpZipLib/issues/471.
    /// </remarks>
    [Fact]
    public void ZipFileAESReadWithEmptyPassword()
    {
        var fileBytes = Convert.FromBase64String(TestFileWithEmptyPassword);

        using var ms = new MemoryStream(fileBytes);
        using var zipFile = new ZipFile(ms, leaveOpen: true);
        zipFile.Password = string.Empty;

        var entry = zipFile.FindEntry("test", true);

        using var inputStream = zipFile.GetInputStream(entry);
        using var sr = new StreamReader(inputStream, Encoding.UTF8);
        var content = sr.ReadToEnd();
        Assert.Equal("Lorem ipsum dolor sit amet, consectetur adipiscing elit.", content);
    }

    /// <summary>
    /// ZipInputStream can't decrypt AES encrypted entries, but it should report that to the caller
    /// rather than just failing.
    /// </summary>
    [Fact]
    public void ZipinputStreamShouldGracefullyFailWithAESStreams()
    {
        var password = "password";

        using var memoryStream = new MemoryStream();
        // Try to create a zip stream
        WriteEncryptedZipToStream(memoryStream, password, 256);

        // reset
        memoryStream.Seek(0, SeekOrigin.Begin);

        // Try to read
        using var inputStream = new ZipInputStream(memoryStream);
        inputStream.Password = password;
        var entry = inputStream.GetNextEntry();
        Assert.Equal(256, entry.AESKeySize);

        // CanDecompressEntry should be false.
        Assert.False(inputStream.CanDecompressEntry, "CanDecompressEntry should be false for AES encrypted entries");

        // Should throw on read.
        Assert.Throws<ZipException>(() => inputStream.ReadByte());
    }

    private static void WriteEncryptedZipToStream(Stream stream, string password, int keySize, CompressionMethod compressionMethod = CompressionMethod.Deflated)
    {
        using var zs = new ZipOutputStream(stream);
        zs.IsStreamOwner = false;
        zs.SetLevel(9); // 0-9, 9 being the highest level of compression
        zs.Password = password;  // optional. Null is the same as not setting. Required if using AES.

        AddEncryptedEntryToStream(zs, $"test", keySize, compressionMethod);
    }

    private void WriteEncryptedZipToStream(Stream stream, int entryCount, string password, int keySize, CompressionMethod compressionMethod)
    {
        using var zs = new ZipOutputStream(stream);
        zs.IsStreamOwner = false;
        zs.SetLevel(9); // 0-9, 9 being the highest level of compression
        zs.Password = password;  // optional. Null is the same as not setting. Required if using AES.

        for (var i = 0;  i < entryCount; i++)
        {
            AddEncryptedEntryToStream(zs, $"test-{i}", keySize, compressionMethod);
        }
    }

    private static void AddEncryptedEntryToStream(ZipOutputStream zipOutputStream, string entryName, int keySize, CompressionMethod compressionMethod)
    {
        var zipEntry = new ZipEntry(entryName)
        {
            AESKeySize = keySize,
            DateTime = DateTime.Now,
            CompressionMethod = compressionMethod
        };

        zipOutputStream.PutNextEntry(zipEntry);

        var dummyData = Encoding.UTF8.GetBytes(DummyDataString);

        using (var dummyStream = new MemoryStream(dummyData))
        {
            dummyStream.CopyTo(zipOutputStream);
        }

        zipOutputStream.CloseEntry();
    }

    private void CreateZipWithEncryptedEntries(string password, int keySize, CompressionMethod compressionMethod = CompressionMethod.Deflated)
    {
        using var ms = new MemoryStream();
        WriteEncryptedZipToStream(ms, password, keySize, compressionMethod);
        SevenZipHelper.VerifyZipWith7Zip(ms, password);
    }

    private const string DummyDataString = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit.
Fusce bibendum diam ac nunc rutrum ornare. Maecenas blandit elit ligula, eget suscipit lectus rutrum eu.
Maecenas aliquam, purus mattis pulvinar pharetra, nunc orci maximus justo, sed facilisis massa dui sed lorem.
Vestibulum id iaculis leo. Duis porta ante lorem. Duis condimentum enim nec lorem tristique interdum. Fusce in faucibus libero.";
}
