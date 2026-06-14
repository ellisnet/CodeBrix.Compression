using CodeBrix.Compression.GZip;
using CodeBrix.Compression.Tests.TestSupport;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace CodeBrix.Compression.Tests.GZip;

/// <summary>
/// This class contains test cases for GZip compression
/// </summary>
[Trait("Category", "GZip")]
public class GZipTestSuite
{
    /// <summary>
    /// Basic compress/decompress test
    /// </summary>
    [Fact]
    public void TestGZip()
    {
        var ms = new MemoryStream();
        var outStream = new GZipOutputStream(ms);

        var buf = Utils.GetDummyBytes(size: 100000);

        outStream.Write(buf, 0, buf.Length);
        outStream.Flush();
        outStream.Finish();

        ms.Seek(0, SeekOrigin.Begin);

        var inStream = new GZipInputStream(ms);
        var buf2 = new byte[buf.Length];
        var currentIndex = 0;
        var count = buf2.Length;

        while (true)
        {
            var numRead = inStream.Read(buf2, currentIndex, count);
            if (numRead <= 0)
            {
                break;
            }
            currentIndex += numRead;
            count -= numRead;
        }

        Assert.Equal(0, count);

        for (var i = 0; i < buf.Length; ++i)
        {
            Assert.Equal(buf2[i], buf[i]);
        }
    }

    /// <summary>
    /// Writing GZip headers is delayed so that this stream can be used with HTTP/IIS.
    /// </summary>
    [Fact]
    public void DelayedHeaderWriteNoData()
    {
        using var ms = new MemoryStream();
        Assert.Equal(0, ms.Length);

        using (new GZipOutputStream(ms))
        {
            Assert.Equal(0, ms.Length);
        }

        Assert.NotEmpty(ms.ToArray());
    }


    /// <summary>
    /// Variant of DelayedHeaderWriteNoData testing flushing for https://github.com/icsharpcode/SharpZipLib/issues/382
    /// </summary>
    [Fact]
    public void DelayedHeaderWriteFlushNoData()
    {
        var ms = new MemoryStream();
        Assert.Equal(0, ms.Length);

        using (var outStream = new GZipOutputStream(ms) { IsStreamOwner = false })
        {
            // #382 - test flushing the stream before writing to it.
            outStream.Flush();
        }

        ms.Seek(0, SeekOrigin.Begin);

        // Test that the gzip stream can be read
        var readStream = new MemoryStream();
        using (var inStream = new GZipInputStream(ms))
        {
            inStream.CopyTo(readStream);
        }

        var data = readStream.ToArray();

        Assert.Empty(data);
    }

    /// <summary>
    /// Writing GZip headers is delayed so that this stream can be used with HTTP/IIS.
    /// </summary>
    [Fact]
    public void DelayedHeaderWriteWithData()
    {
        var ms = new MemoryStream();
        Assert.Equal(0, ms.Length);
        using (var outStream = new GZipOutputStream(ms))
        {
            Assert.Equal(0, ms.Length);
            outStream.WriteByte(45);

            // Should in fact contain header right now with
            // 1 byte in the compression pipeline
            Assert.Equal(10, ms.Length);
        }
        var data = ms.ToArray();

        Assert.True(data.Length > 0);
    }

    /// <summary>
    /// variant of DelayedHeaderWriteWithData to test https://github.com/icsharpcode/SharpZipLib/issues/382
    /// </summary>
    [Fact]
    public void DelayedHeaderWriteFlushWithData()
    {
        var ms = new MemoryStream();
        Assert.Equal(0, ms.Length);
        using (var outStream = new GZipOutputStream(ms) { IsStreamOwner = false })
        {
            Assert.Equal(0, ms.Length);

            // #382 - test flushing the stream before writing to it.
            outStream.Flush();
            outStream.WriteByte(45);
        }

        ms.Seek(0, SeekOrigin.Begin);

        // Test that the gzip stream can be read
        var readStream = new MemoryStream();
        using (var inStream = new GZipInputStream(ms))
        {
            inStream.CopyTo(readStream);
        }

        // Check that the data was read
        var data = readStream.ToArray();
        Assert.Equal(new byte[] { 45 }, data);
    }

    [Fact]
    public void ZeroLengthInputStream()
    {
        var gzi = new GZipInputStream(new MemoryStream());
        var exception = false;
        var retval = int.MinValue;
        try
        {
            retval = gzi.ReadByte();
        }
        catch
        {
            exception = true;
        }

        Assert.False(exception, "reading from an empty stream should not cause an exception");
        Assert.Equal(-1, retval);
    }

    [Fact]
    public void OutputStreamOwnership()
    {
        var memStream = new TrackedMemoryStream();
        var s = new GZipOutputStream(memStream);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.Close();

        Assert.True(memStream.IsClosed, "Should be closed after parent owner close");
        Assert.True(memStream.IsDisposed, "Should be disposed after parent owner close");

        memStream = new TrackedMemoryStream();
        s = new GZipOutputStream(memStream);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.IsStreamOwner = false;
        s.Close();

        Assert.False(memStream.IsClosed, "Should not be closed after parent owner close");
        Assert.False(memStream.IsDisposed, "Should not be disposed after parent owner close");
    }

    [Fact]
    public void InputStreamOwnership()
    {
        var memStream = new TrackedMemoryStream();
        var s = new GZipInputStream(memStream);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.Close();

        Assert.True(memStream.IsClosed, "Should be closed after parent owner close");
        Assert.True(memStream.IsDisposed, "Should be disposed after parent owner close");

        memStream = new TrackedMemoryStream();
        s = new GZipInputStream(memStream);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.IsStreamOwner = false;
        s.Close();

        Assert.False(memStream.IsClosed, "Should not be closed after parent owner close");
        Assert.False(memStream.IsDisposed, "Should not be disposed after parent owner close");
    }

    [Fact]
    public void DoubleFooter()
    {
        var memStream = new TrackedMemoryStream();
        var s = new GZipOutputStream(memStream);
        s.Finish();
        var length = memStream.Length;
        s.Close();
        Assert.Equal(length, memStream.ToArray().Length);
    }

    [Fact]
    public void DoubleClose()
    {
        var memStream = new TrackedMemoryStream();
        var s = new GZipOutputStream(memStream);
        s.Finish();
        s.Close();
        s.Close();

        memStream = new TrackedMemoryStream();
        using (new GZipOutputStream(memStream))
        {
            s.Close();
        }
    }

    [Fact]
    public void WriteAfterFinish()
    {
        var memStream = new TrackedMemoryStream();
        var s = new GZipOutputStream(memStream);
        s.Finish();

        Assert.Throws<InvalidOperationException>(() => s.WriteByte(value: 7));
    }

    [Fact]
    public void WriteAfterClose()
    {
        var memStream = new TrackedMemoryStream();
        var s = new GZipOutputStream(memStream);
        s.Close();

        Assert.Throws<InvalidOperationException>(() => s.WriteByte(value: 7));
    }

    /// <summary>
    /// Verify that if a decompression was successful for at least one block we're exiting gracefully.
    /// </summary>
    [Fact]
    public void TrailingGarbage()
    {
        /* ARRANGE */
        var ms = new MemoryStream();
        var outStream = new GZipOutputStream(ms);

        // input buffer to be compressed
        var buf = Utils.GetDummyBytes(size: 100000, seed: 3);

        // compress input buffer
        outStream.Write(buf, 0, buf.Length);
        outStream.Flush();
        outStream.Finish();

        // generate random trailing garbage and add to the compressed stream
        Utils.WriteDummyData(ms, size: 4096, seed: 4);

        // rewind the concatenated stream
        ms.Seek(0, SeekOrigin.Begin);

        /* ACT */
        // decompress concatenated stream
        var inStream = new GZipInputStream(ms);
        var buf2 = new byte[buf.Length];
        var currentIndex = 0;
        var count = buf2.Length;
        while (true)
        {
            var numRead = inStream.Read(buf2, currentIndex, count);
            if (numRead <= 0)
            {
                break;
            }
            currentIndex += numRead;
            count -= numRead;
        }

        /* ASSERT */
        Assert.Equal(0, count);
        for (var i = 0; i < buf.Length; ++i)
        {
            Assert.Equal(buf2[i], buf[i]);
        }
    }

    /// <summary>
    /// Test that if we flush a GZip output stream then all data that has been written
    /// is flushed through to the underlying stream and can be successfully read back
    /// even if the stream is not yet finished.
    /// </summary>
    [Fact]
    public void FlushToUnderlyingStream()
    {
        var ms = new MemoryStream();
        var outStream = new GZipOutputStream(ms);

        var buf = Utils.GetDummyBytes(size: 100000);

        outStream.Write(buf, 0, buf.Length);
        // Flush output stream but don't finish it yet
        outStream.Flush();

        ms.Seek(0, SeekOrigin.Begin);

        var inStream = new GZipInputStream(ms);
        var buf2 = new byte[buf.Length];
        var currentIndex = 0;
        var count = buf2.Length;

        while (true)
        {
            try
            {
                var numRead = inStream.Read(buf2, currentIndex, count);
                if (numRead <= 0)
                {
                    break;
                }
                currentIndex += numRead;
                count -= numRead;
            }
            catch (GZipException)
            {
                // We should get an unexpected EOF exception once we've read all
                // data as the stream isn't yet finished.
                break;
            }
        }

        Assert.Equal(0, count);

        for (var i = 0; i < buf.Length; ++i)
        {
            Assert.Equal(buf2[i], buf[i]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void SmallBufferDecompression(int seed)
    {
        var outputBufferSize = 100000;
        var outputBuffer = new byte[outputBufferSize];
        var inputBuffer = Utils.GetDummyBytes(outputBufferSize * 4, seed);

        using var msGzip = new MemoryStream();
        using (var gzos = new GZipOutputStream(msGzip){IsStreamOwner = false})
        {
            gzos.Write(inputBuffer, 0, inputBuffer.Length);
        }

        msGzip.Seek(0, SeekOrigin.Begin);

        using (var gzis = new GZipInputStream(msGzip))
        using (var msRaw = new MemoryStream())
        {
            int readOut;
            while ((readOut = gzis.Read(outputBuffer, 0, outputBuffer.Length)) > 0)
            {
                msRaw.Write(outputBuffer, 0, readOut);
            }

            var resultBuffer = msRaw.ToArray();
            for (var i = 0; i < resultBuffer.Length; i++)
            {
                Assert.Equal(inputBuffer[i], resultBuffer[i]);
            }
        }
    }

    /// <summary>
    /// Should gracefully handle reading from a stream that becomes unreadable after
    ///  all of the data has been read.
    /// </summary>
    /// <remarks>
    /// Test for https://github.com/icsharpcode/SharpZipLib/issues/379
    /// </remarks>
    [Fact]
    [Trait("Category", "Zip")]
    public void ShouldGracefullyHandleReadingANonReadableStream()
    {
        MemoryStream ms = new SelfClosingStream();
        using (var gzos = new GZipOutputStream(ms))
        {
            gzos.IsStreamOwner = false;
            Utils.WriteDummyData(gzos, size: 100000);
        }

        ms.Seek(0, SeekOrigin.Begin);

        using (var gzis = new GZipInputStream(ms))
        using (var msRaw = new MemoryStream())
        {
            gzis.CopyTo(msRaw);
        }
    }

    [Fact(Explicit = true, Skip = "Long Running")]
    [Trait("Category", "Performance")]
    [Trait("Category", "Long Running")]
    public void WriteThroughput()
    {
        PerformanceTesting.TestWrite(
            size: TestDataSize.Large,
            output: w => new GZipOutputStream(w)
        );
    }

    [Fact(Explicit = true, Skip = "Long Running")]
    [Trait("Category", "Performance")]
    public void ReadWriteThroughput()
    {
        PerformanceTesting.TestReadWrite(
            size: TestDataSize.Large,
            input: w => new GZipInputStream(w),
            output: w => new GZipOutputStream(w)
        );
    }

    /// <summary>
    /// Basic compress/decompress test
    /// </summary>
    [Fact]
    public void OriginalFilename()
    {
        var content = "FileContents";


        using var ms = new MemoryStream();
        using (var outStream = new GZipOutputStream(ms) { IsStreamOwner = false })
        {
            outStream.FileName = "/path/to/file.ext";

            var writeBuffer = Encoding.ASCII.GetBytes(content);
            outStream.Write(writeBuffer, 0, writeBuffer.Length);
            outStream.Flush();
            outStream.Finish();
        }

        ms.Seek(0, SeekOrigin.Begin);

        using (var inStream = new GZipInputStream(ms))
        {
            var readBuffer = new byte[content.Length];
            inStream.ReadExactly(readBuffer, 0, readBuffer.Length);
            Assert.Equal(content, Encoding.ASCII.GetString(readBuffer));
            Assert.Equal("file.ext", inStream.GetFilename());
        }
    }
}
