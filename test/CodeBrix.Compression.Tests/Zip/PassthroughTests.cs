using CodeBrix.Compression.Checksum;
using CodeBrix.Compression.Tests.TestSupport;
using CodeBrix.Compression.Zip;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace CodeBrix.Compression.Tests.Zip;

[Trait("Category", "Zip")]
public class PassthroughTests
{
    [Fact]
    public void AddingValidPrecompressedEntryToZipOutputStream()
    {
        using var ms = new MemoryStream();

        using (var outStream = new ZipOutputStream(ms){IsStreamOwner = false})
        {
            var (compressedData, crc, size) = CreateDeflatedData();
            var entry = new ZipEntry("dummyfile.tst")
            {
                CompressionMethod = CompressionMethod.Deflated,
                Size = size,
                Crc = (uint)crc.Value,
                CompressedSize = compressedData.Length,
            };

            outStream.PutNextPassthroughEntry(entry);

            compressedData.CopyTo(outStream);
        }

        ZipTesting.AssertPassesTestArchive(ms.ToArray());
    }

    private static (MemoryStream, Crc32, int) CreateDeflatedData()
    {
        var data = Encoding.UTF8.GetBytes("Hello, world");

        var crc = new Crc32();
        crc.Update(data);

        var compressedData = new MemoryStream();
        using(var gz = new DeflateStream(compressedData, CompressionMode.Compress, leaveOpen: true))
        {
            gz.Write(data, 0, data.Length);
        }
        compressedData.Position = 0;

        return (compressedData, crc, data.Length);
    }

    [Fact]
    public void AddingPrecompressedEntryToZipOutputStreamWithInvalidSize()
    {
        using var outStream = new ZipOutputStream(new MemoryStream());
        var (compressedData, crc, size) = CreateDeflatedData();
        outStream.Password = "mockpassword";
        var entry = new ZipEntry("dummyfile.tst")
        {
            CompressionMethod = CompressionMethod.Stored,
            Crc = (uint)crc.Value,
            CompressedSize = compressedData.Length,
        };

        Assert.Throws<ZipException>(() =>
        {
            outStream.PutNextPassthroughEntry(entry);
        });
    }


    [Fact]
    public void AddingPrecompressedEntryToZipOutputStreamWithInvalidCompressedSize()
    {
        using var outStream = new ZipOutputStream(new MemoryStream());
        var (compressedData, crc, size) = CreateDeflatedData();
        outStream.Password = "mockpassword";
        var entry = new ZipEntry("dummyfile.tst")
        {
            CompressionMethod = CompressionMethod.Stored,
            Size = size,
            Crc = (uint)crc.Value,
        };

        Assert.Throws<ZipException>(() =>
        {
            outStream.PutNextPassthroughEntry(entry);
        });
    }

    [Fact]
    public void AddingPrecompressedEntryToZipOutputStreamWithNonSupportedMethod()
    {
        using var outStream = new ZipOutputStream(new MemoryStream());
        var (compressedData, crc, size) = CreateDeflatedData();
        outStream.Password = "mockpassword";
        var entry = new ZipEntry("dummyfile.tst")
        {
            CompressionMethod = CompressionMethod.LZMA,
            Size = size,
            Crc = (uint)crc.Value,
            CompressedSize = compressedData.Length,
        };

        Assert.Throws<NotImplementedException>(() =>
        {
            outStream.PutNextPassthroughEntry(entry);
        });
    }

    [Fact]
    public void AddingPrecompressedEntryToZipOutputStreamWithEncryption()
    {
        using var outStream = new ZipOutputStream(new MemoryStream());
        var (compressedData, crc, size) = CreateDeflatedData();
        outStream.Password = "mockpassword";
        var entry = new ZipEntry("dummyfile.tst")
        {
            CompressionMethod = CompressionMethod.Deflated,
            Size = size,
            Crc = (uint)crc.Value,
            CompressedSize = compressedData.Length,
        };

        Assert.Throws<NotImplementedException>(() =>
        {
            outStream.PutNextPassthroughEntry(entry);
        });
    }
}
