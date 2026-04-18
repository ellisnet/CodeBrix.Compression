using CodeBrix.Compression.Core;
using CodeBrix.Compression.Tests.TestSupport;
using CodeBrix.Compression.Zip;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CodeBrix.Compression.Tests.Zip;

/// <summary>
/// This contains newer tests for stream handling. Much of this is still in GeneralHandling
/// </summary>
[Trait("Category", "Zip")]
public class StreamHandling : ZipBase, IDisposable
{
    private TestTraceListener Listener;

    public StreamHandling()
    {
        Trace.Listeners.Add(Listener = new TestTraceListener(Console.Out));
    }

    public void Dispose() => Trace.Listeners.Remove(Listener);

    private void MustFailRead(Stream s, byte[] buffer, int offset, int count)
    {
        var exception = false;
        try
        {
            s.ReadExactly(buffer, offset, count);
        }
        catch
        {
            exception = true;
        }
        Assert.True(exception, "Read should fail");
    }

    [Fact]
    public void ParameterHandling()
    {
        var buffer = new byte[10];
        var emptyBuffer = new byte[0];

        var ms = new MemoryStream();
        var outStream = new ZipOutputStream(ms);
        outStream.IsStreamOwner = false;
        outStream.PutNextEntry(new ZipEntry("Floyd"));
        outStream.Write(buffer, 0, 10);
        outStream.Finish();

        ms.Seek(0, SeekOrigin.Begin);

        var inStream = new ZipInputStream(ms);
        inStream.GetNextEntry();

        MustFailRead(inStream, buffer: null, 0, 0);
        MustFailRead(inStream, buffer, -1, 1);
        MustFailRead(inStream, buffer, 0, 11);
        MustFailRead(inStream, buffer, 7, 5);
        MustFailRead(inStream, buffer, 0, -1);

        MustFailRead(inStream, emptyBuffer, 0, 1);

        var bytesRead = inStream.Read(buffer, 10, 0);
        Assert.Equal(0, bytesRead);

        bytesRead = inStream.Read(emptyBuffer, 0, 0);
        Assert.Equal(0, bytesRead);
    }

    /// <summary>
    /// Check that Zip64 descriptor is added to an entry OK.
    /// </summary>
    [Fact]
    public void Zip64Descriptor()
    {
        MemoryStream msw = new MemoryStreamWithoutSeek();
        var outStream = new ZipOutputStream(msw);
        outStream.UseZip64 = UseZip64.Off;

        outStream.IsStreamOwner = false;
        outStream.PutNextEntry(new ZipEntry("StripedMarlin"));
        outStream.WriteByte(89);
        outStream.Close();

        ZipTesting.AssertPassesTestArchive(msw.ToArray());

        msw = new MemoryStreamWithoutSeek();
        outStream = new ZipOutputStream(msw);
        outStream.UseZip64 = UseZip64.On;

        outStream.IsStreamOwner = false;
        outStream.PutNextEntry(new ZipEntry("StripedMarlin"));
        outStream.WriteByte(89);
        outStream.Close();

        ZipTesting.AssertPassesTestArchive(msw.ToArray());
    }

    [Fact]
    public void ReadAndWriteZip64NonSeekable()
    {
        MemoryStream msw = new MemoryStreamWithoutSeek();
        using (var outStream = new ZipOutputStream(msw))
        {
            outStream.UseZip64 = UseZip64.On;

            outStream.IsStreamOwner = false;
            outStream.PutNextEntry(new ZipEntry("StripedMarlin"));
            outStream.WriteByte(89);

            outStream.PutNextEntry(new ZipEntry("StripedMarlin2"));
            outStream.WriteByte(89);

            outStream.Close();
        }

        var msBytes = msw.ToArray();
        ZipTesting.AssertPassesTestArchive(msBytes);

        using (var zis = new ZipInputStream(new MemoryStream(msBytes)))
        {
            while (zis.GetNextEntry() != null)
            {
                const int bufferSize = 1024;
                var buffer = new byte[bufferSize];
                while (zis.Read(buffer, 0, bufferSize) > 0)
                {
                    // Reading the data is enough
                }
            }
        }
    }

    /// <summary>
    /// Check that adding an entry with no data and Zip64 works OK
    /// </summary>
    [Fact]
    public void EntryWithNoDataAndZip64()
    {
        MemoryStream msw = new MemoryStreamWithoutSeek();
        var outStream = new ZipOutputStream(msw);

        outStream.IsStreamOwner = false;
        var ze = new ZipEntry("Striped Marlin");
        ze.ForceZip64();
        ze.Size = 0;
        outStream.PutNextEntry(ze);
        outStream.CloseEntry();
        outStream.Finish();
        outStream.Close();

        ZipTesting.AssertPassesTestArchive(msw.ToArray());
    }

    /// <summary>
    /// Empty zip entries can be created and read?
    /// </summary>

    [Fact]
    public void EmptyZipEntries()
    {
        var ms = new MemoryStream();
        var outStream = new ZipOutputStream(ms);

        for (var i = 0; i < 10; ++i)
        {
            outStream.PutNextEntry(new ZipEntry(i.ToString()));
        }

        outStream.Finish();

        ms.Seek(0, SeekOrigin.Begin);

        var inStream = new ZipInputStream(ms);

        var extractCount = 0;
        var decompressedData = new byte[100];

        while ((inStream.GetNextEntry()) != null)
        {
            while (true)
            {
                var numRead = inStream.Read(decompressedData, extractCount, decompressedData.Length);
                if (numRead <= 0)
                {
                    break;
                }
                extractCount += numRead;
            }
        }
        inStream.Close();
        Assert.Equal(0, extractCount);
    }

    /// <summary>
    /// Test that calling Write with 0 bytes behaves.
    /// See issue @ https://github.com/icsharpcode/SharpZipLib/issues/123.
    /// </summary>
    [Fact]
    public void TestZeroByteWrite()
    {
        using var ms = new MemoryStreamWithoutSeek();
        using (var outStream = new ZipOutputStream(ms) { IsStreamOwner = false })
        {
            var ze = new ZipEntry("Striped Marlin");
            outStream.PutNextEntry(ze);

            var buffer = Array.Empty<byte>();
            outStream.Write(buffer, 0, 0);
        }

        ms.Seek(0, SeekOrigin.Begin);

        using (var inStream = new ZipInputStream(ms) { IsStreamOwner = false })
        {
            var extractCount = 0;
            var decompressedData = new byte[100];

            while (inStream.GetNextEntry() != null)
            {
                while (true)
                {
                    var numRead = inStream.Read(decompressedData, extractCount, decompressedData.Length);
                    if (numRead <= 0)
                    {
                        break;
                    }
                    extractCount += numRead;
                }
            }
            Assert.Equal(0, extractCount);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(256)]
    public void WriteZipStreamWithNoCompression(int contentLength)
    {
        var buffer = new byte[255];

        using var dummyZip = Utils.GetTempFile();
        using var inputFile = Utils.GetDummyFile(contentLength);
        // Filename is manually cleaned here to prevent this test from failing while ZipEntry doesn't automatically clean it
        var inputFileName = ZipEntry.CleanName(inputFile);

        using (var zipFileStream = File.OpenWrite(dummyZip))
        using (var zipOutputStream = new ZipOutputStream(zipFileStream))
        using (var inputFileStream = File.OpenRead(inputFile))
        {
            zipOutputStream.PutNextEntry(new ZipEntry(inputFileName)
            {
                CompressionMethod = CompressionMethod.Stored,
            });

            StreamUtils.Copy(inputFileStream, zipOutputStream, buffer);
        }

        using (var zf = new ZipFile(dummyZip))
        {
            var inputBytes = File.ReadAllBytes(inputFile);

            var entry = zf.GetEntry(inputFileName);
            Assert.NotNull(entry);

            var ex = Record.Exception(() =>
            {
                using var entryStream = zf.GetInputStream(entry);
                var outputBytes = new byte[entryStream.Length];
                entryStream.ReadExactly(outputBytes, 0, outputBytes.Length);

                Assert.Equal(inputBytes, outputBytes);
            });
            Assert.True(ex == null, $"Failed to locate entry stream in archive: {ex}");

            ZipTesting.AssertPassesTestArchive(zf);
        }
    }

    [Fact]
    public void ZipEntryFileNameAutoClean()
    {
        using var dummyZip = Utils.GetDummyFile(0);
        using var inputFile = Utils.GetDummyFile();
        using (var zipFileStream = File.OpenWrite(dummyZip))
        using (var zipOutputStream = new ZipOutputStream(zipFileStream))
        using (var inputFileStream = File.OpenRead(inputFile))
        {
            // New ZipEntry created with a full file name path as it's name
            zipOutputStream.PutNextEntry(new ZipEntry(inputFile)
            {
                CompressionMethod = CompressionMethod.Stored,
            });

            inputFileStream.CopyTo(zipOutputStream);
        }

        using (var zf = new ZipFile(dummyZip))
        {
            // The ZipEntry name should have been automatically cleaned
            Assert.Equal(ZipEntry.CleanName(inputFile), zf[0].Name);
        }
    }

    /// <summary>
    /// Empty zips can be created and read?
    /// </summary>
    [Fact]
    public void CreateAndReadEmptyZip()
    {
        var ms = new MemoryStream();
        var outStream = new ZipOutputStream(ms);
        outStream.Finish();

        ms.Seek(0, SeekOrigin.Begin);

        var inStream = new ZipInputStream(ms);
        while ((inStream.GetNextEntry()) != null)
        {
            Assert.Fail("No entries should be found in empty zip");
        }
    }

    /// <summary>
    /// Base stream is closed when IsOwner is true ( default);
    /// </summary>
    [Fact]
    public void BaseClosedWhenOwner()
    {
        var ms = new TrackedMemoryStream();

        Assert.False(ms.IsClosed, "Underlying stream should NOT be closed");

        using (var stream = new ZipOutputStream(ms))
        {
            Assert.True(stream.IsStreamOwner, "Should be stream owner by default");
        }

        Assert.True(ms.IsClosed, "Underlying stream should be closed");
    }

    /// <summary>
    /// Check that base stream is not closed when IsOwner is false;
    /// </summary>
    [Fact]
    public void BaseNotClosedWhenNotOwner()
    {
        var ms = new TrackedMemoryStream();

        Assert.False(ms.IsClosed, "Underlying stream should NOT be closed");

        using (var stream = new ZipOutputStream(ms))
        {
            Assert.True(stream.IsStreamOwner, "Should be stream owner by default");
            stream.IsStreamOwner = false;
        }
        Assert.False(ms.IsClosed, "Underlying stream should still NOT be closed");
    }

    /// <summary>
    /// Check that base stream is not closed when IsOwner is false;
    /// </summary>
    [Fact]
    public void BaseClosedAfterFailure()
    {
        var ms = new TrackedMemoryStream(new byte[32]);

        Assert.False(ms.IsClosed, "Underlying stream should NOT be closed initially");
        var blewUp = false;
        try
        {
            using var stream = new ZipOutputStream(ms);
            Assert.True(stream.IsStreamOwner, "Should be stream owner by default");
            try
            {
                stream.PutNextEntry(new ZipEntry("Tiny"));
                stream.Write(new byte[32], 0, 32);
            }
            finally
            {
                Assert.False(ms.IsClosed, "Stream should still not be closed.");
                stream.Close();
                Assert.Fail("Exception not thrown");
            }
        }
        catch
        {
            blewUp = true;
        }

        Assert.True(blewUp, "Should have failed to write to stream");
        Assert.True(ms.IsClosed, "Underlying stream should be closed");
    }

    [Fact(Explicit = true, Skip = "Long Running")]
    [Trait("Category", "Performance")]
    public void WriteThroughput()
    {
        PerformanceTesting.TestWrite(0x10000000, bs =>
        {
            var zos = new ZipOutputStream(bs);
            zos.PutNextEntry(new ZipEntry("0"));
            return zos;
        });
    }

    [Fact(Explicit = true, Skip = "Long Running")]
    [Trait("Category", "Performance")]
    public void SingleLargeEntry()
    {
        const string entryName = "CantSeek";

        PerformanceTesting.TestReadWrite(
            size: TestDataSize.Large,
            input: bs =>
            {
                var zis = new ZipInputStream(bs);
                var entry = zis.GetNextEntry();

                Assert.Equal(entryName, entry.Name);
                Assert.True((entry.Flags & (int)GeneralBitFlags.Descriptor) != 0);
                return zis;
            },
            output: bs =>
            {
                var zos = new ZipOutputStream(bs);
                zos.PutNextEntry(new ZipEntry(entryName));
                return zos;
            }
        );
    }

    const string BZip2CompressedZip =
        "UEsDBC4AAAAMAEyxgU5p3ou9JwAAAAcAAAAFAAAAYS5kYXRCWmg5MUFZJlNZ0buMcAAAAkgACABA" +
        "ACAAIQCCCxdyRThQkNG7jHBQSwECMwAuAAAADABMsYFOad6LvScAAAAHAAAABQAAAAAAAAAAAAAA" +
        "AAAAAAAAYS5kYXRQSwUGAAAAAAEAAQAzAAAASgAAAAAA";

    /// <summary>
    /// Should fail to read a zip with BZip2 compression
    /// </summary>
    [Fact]
    public void ShouldReadBZip2EntryButNotDecompress()
    {
        var fileBytes = Convert.FromBase64String(BZip2CompressedZip);

        using var input = new MemoryStream(fileBytes, writable: false);
        var zis = new ZipInputStream(input);
        var entry = zis.GetNextEntry();

        Assert.Equal("a.dat", entry.Name);
        Assert.Equal(CompressionMethod.BZip2, entry.CompressionMethod);
        Assert.False(zis.CanDecompressEntry, "Should not be able to decompress BZip2 entry");

        var buffer = new byte[1];
        Assert.Throws<ZipException>(() => zis.ReadExactly(buffer, 0, 1));
    }

    /// <summary>
    /// Test for https://github.com/icsharpcode/SharpZipLib/issues/341
    /// Should be able to read entries whose names contain invalid filesystem
    /// characters
    /// </summary>
    [Fact]
    public void ShouldBeAbleToReadEntriesWithInvalidFileNames()
    {
        var testFileName = "<A|B?C>.txt";

        using var memoryStream = new MemoryStream();
        using (var outStream = new ZipOutputStream(memoryStream))
        {
            outStream.IsStreamOwner = false;
            outStream.PutNextEntry(new ZipEntry(testFileName));
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        using (var inStream = new ZipInputStream(memoryStream))
        {
            var entry = inStream.GetNextEntry();
            Assert.Equal(testFileName, entry.Name);
        }
    }

    /// <summary>
    /// Test for https://github.com/icsharpcode/SharpZipLib/issues/507
    /// </summary>
    [Fact]
    public void AddingAnAESEntryWithNoPasswordShouldThrow()
    {
        using var memoryStream = new MemoryStream();
        using var outStream = new ZipOutputStream(memoryStream);
        var newEntry = new ZipEntry("test") { AESKeySize = 256 };

        Assert.Throws<InvalidOperationException>(() => outStream.PutNextEntry(newEntry));
    }

    [Fact]
    public void ShouldThrowDescriptiveExceptionOnUncompressedDescriptorEntry()
    {
        using var ms = new MemoryStreamWithoutSeek();
        using (var zos = new ZipOutputStream(ms))
        {
            zos.IsStreamOwner = false;
            var entry = new ZipEntry("testentry");
            entry.CompressionMethod = CompressionMethod.Stored;
            entry.Flags |= (int)GeneralBitFlags.Descriptor;
            zos.PutNextEntry(entry);
            zos.Write(new byte[1], 0, 1);
            zos.CloseEntry();
        }

        // Patch the Compression Method, since ZipOutputStream automatically changes it to Deflate when descriptors are used
        ms.Seek(8, SeekOrigin.Begin);
        ms.WriteByte((byte)CompressionMethod.Stored);
        ms.Seek(0, SeekOrigin.Begin);

        using (var zis = new ZipInputStream(ms))
        {
            zis.IsStreamOwner = false;
            var buf = new byte[32];
            zis.GetNextEntry();

            Assert.Throws<StreamUnsupportedException>(() =>
            {
                zis.ReadExactly(buf, 0, buf.Length);
            });
        }
    }

    [Theory]
    [InlineData((byte)0x0)]
    [InlineData((byte)0x80)]
    public void IteratingOverEntriesInDirectUpdatedArchive(byte padding)
    {
        using var tempFile = new TempFile();
        using (var zf = ZipFile.Create(tempFile))
        {
            zf.BeginUpdate();
            // Add a "large" file, where the bottom 1023 bytes will become padding
            var contentsAndPadding = Enumerable.Repeat(padding, count: 1024).ToArray();
            zf.Add(new MemoryDataSource(contentsAndPadding), "FirstFile", CompressionMethod.Stored);
            // Add a second file after the first one
            zf.Add(new StringMemoryDataSource("fileContents"), "SecondFile", CompressionMethod.Stored);
            zf.CommitUpdate();
        }

        // Since ZipFile doesn't support UpdateCommand.Modify yet we'll have to simulate it by patching the header
        Utils.PatchFirstEntrySize(tempFile.Open(FileMode.Open), 1);

        // Iterate updated entries
        using (var fs = File.OpenRead(tempFile))
        using (var zis = new ZipInputStream(fs))
        {
            var firstEntry = zis.GetNextEntry();
            Assert.NotNull(firstEntry);
            Assert.Equal(1, firstEntry.CompressedSize);
            Assert.Equal(1, firstEntry.Size);

            var secondEntry = zis.GetNextEntry();
            Assert.NotNull(secondEntry);
            var contents = new StreamReader(zis, Encoding.UTF8, false, 128, true).ReadToEnd();
            Assert.Equal("fileContents", contents);
        }
    }
}
