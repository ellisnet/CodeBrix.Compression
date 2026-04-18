using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CodeBrix.Compression.GZip;
using CodeBrix.Compression.Tests.TestSupport;
using Xunit;

namespace CodeBrix.Compression.Tests.GZip;

[Trait("Category", "GZip")]
[Trait("Category", "Async")]
public class GZipAsyncTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task SmallBufferDecompressionAsync(int seed)
    {
        var outputBufferSize = 100000;
        var outputBuffer = new byte[outputBufferSize];
        var inputBuffer = Utils.GetDummyBytes(outputBufferSize * 4, seed);

        await using var msGzip = new MemoryStream();
        await using (var gzos = new GZipOutputStream(msGzip){IsStreamOwner = false})
        {
            await gzos.WriteAsync(inputBuffer, 0, inputBuffer.Length);
        }

        msGzip.Seek(0, SeekOrigin.Begin);

        await using (var gzis = new GZipInputStream(msGzip))
        await using (var msRaw = new MemoryStream())
        {
            int readOut;
            while ((readOut = gzis.Read(outputBuffer, 0, outputBuffer.Length)) > 0)
            {
                await msRaw.WriteAsync(outputBuffer, 0, readOut);
            }

            var resultBuffer = msRaw.ToArray();
            for (var i = 0; i < resultBuffer.Length; i++)
            {
                Assert.Equal(inputBuffer[i], resultBuffer[i]);
            }
        }
    }

    /// <summary>
    /// Basic compress/decompress test
    /// </summary>
    [Fact]
    public async Task OriginalFilenameAsync()
    {
        var content = "FileContents";

        await using var ms = new MemoryStream();
        await using (var outStream = new GZipOutputStream(ms) { IsStreamOwner = false })
        {
            outStream.FileName = "/path/to/file.ext";
            outStream.Write(Encoding.ASCII.GetBytes(content));
        }
        ms.Seek(0, SeekOrigin.Begin);

        await using (var inStream = new GZipInputStream(ms))
        {
            var readBuffer = new byte[content.Length];
            inStream.ReadExactly(readBuffer, 0, readBuffer.Length);
            Assert.Equal(content, Encoding.ASCII.GetString(readBuffer));
            Assert.Equal("file.ext", inStream.GetFilename());
        }
    }

    /// <summary>
    /// Test creating an empty gzip stream using async
    /// </summary>
    [Fact]
    public async Task EmptyGZipStreamAsync()
    {
        await using var ms = new MemoryStream();
        await using (var outStream = new GZipOutputStream(ms) { IsStreamOwner = false })
        {
            // No content
        }
        ms.Seek(0, SeekOrigin.Begin);

        await using (var inStream = new GZipInputStream(ms))
        using (var reader = new StreamReader(inStream))
        {
            var content = await reader.ReadToEndAsync();
            Assert.Empty(content);
        }
    }

    [Fact]
    public async Task WriteGZipStreamToAsyncOnlyStream()
    {
        var content = Encoding.ASCII.GetBytes("a");
        var modTime = DateTime.UtcNow;

        await using var msAsync = new MemoryStreamWithoutSync();
        await using (var outStream = new GZipOutputStream(msAsync) { IsStreamOwner = false })
        {
            outStream.ModifiedTime = modTime;
            await outStream.WriteAsync(content);
        }

        using var msSync = new MemoryStream();
        await using (var outStream = new GZipOutputStream(msSync) { IsStreamOwner = false })
        {
            outStream.ModifiedTime = modTime;
            outStream.Write(content);
        }

        var syncBytes = string.Join(' ', msSync.ToArray());
        var asyncBytes = string.Join(' ', msAsync.ToArray());

        Assert.Equal(syncBytes, asyncBytes);

        // Since GZipInputStream isn't async yet we need to read from it from a regular MemoryStream
        using (var readStream = new MemoryStream(msAsync.ToArray()))
        await using (var inStream = new GZipInputStream(readStream))
        using (var reader = new StreamReader(inStream))
        {
            // NOTE: preserving original semantics - compare decompressed text to original bytes encoded as ASCII
            Assert.Equal(Encoding.ASCII.GetString(content), await reader.ReadToEndAsync());
        }
    }
}
