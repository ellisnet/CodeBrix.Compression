using CodeBrix.Compression.Checksum;
using System;
using System.Diagnostics;
using Xunit;

namespace CodeBrix.Compression.Tests.Checksum;

[Trait("Category", "Checksum")]
public class ChecksumTests
{
    private readonly
        // Represents ASCII string of "123456789"
        byte[] check = { 49, 50, 51, 52, 53, 54, 55, 56, 57 };

    // Represents string "123456789123456789123456789123456789"
    private readonly byte[] longCheck = {
        49, 50, 51, 52, 53, 54, 55, 56, 57,
        49, 50, 51, 52, 53, 54, 55, 56, 57,
        49, 50, 51, 52, 53, 54, 55, 56, 57,
        49, 50, 51, 52, 53, 54, 55, 56, 57
    };

    [Fact]
    public void Adler_32()
    {
        var underTestAdler32 = new Adler32();
        Assert.Equal(0x00000001L, underTestAdler32.Value);

        underTestAdler32.Update(check);
        Assert.Equal(0x091E01DEL, underTestAdler32.Value);

        underTestAdler32.Reset();
        Assert.Equal(0x00000001L, underTestAdler32.Value);

        exceptionTesting(underTestAdler32);
    }

    const long BufferSize = 256 * 1024 * 1024;

    [Fact]
    public void Adler_32_Performance()
    {
        var rand = new Random(1);

        var buffer = new byte[BufferSize];
        rand.NextBytes(buffer);

        var adler = new Adler32();
        Assert.Equal(0x00000001L, adler.Value);

        var sw = new Stopwatch();
        sw.Start();

        adler.Update(buffer);

        sw.Stop();
        Console.WriteLine($"Adler32 Hashing of 256 MiB: {sw.Elapsed.TotalSeconds:f4} second(s)");

        adler.Update(check);
        Assert.Equal(0xD4897DA3L, adler.Value);

        exceptionTesting(adler);
    }

    [Fact]
    public void CRC_32_BZip2()
    {
        var underTestBZip2Crc = new BZip2Crc();
        Assert.Equal(0x0L, underTestBZip2Crc.Value);

        underTestBZip2Crc.Update(check);
        Assert.Equal(0xFC891918L, underTestBZip2Crc.Value);

        underTestBZip2Crc.Reset();
        Assert.Equal(0x0L, underTestBZip2Crc.Value);

        exceptionTesting(underTestBZip2Crc);
    }

    [Fact]
    public void CRC_32_BZip2_Long()
    {
        var underTestCrc32 = new BZip2Crc();
        underTestCrc32.Update(longCheck);
        Assert.Equal(0xEE53D2B2L, underTestCrc32.Value);
    }

    [Fact]
    public void CRC_32_BZip2_Unaligned()
    {
        // Extract "456" and CRC
        var underTestCrc32 = new BZip2Crc();
        underTestCrc32.Update(new ArraySegment<byte>(check, 3, 3));
        Assert.Equal(0x001D0511L, underTestCrc32.Value);
    }

    [Fact]
    public void CRC_32_BZip2_Long_Unaligned()
    {
        // Extract "789123456789123456" and CRC
        var underTestCrc32 = new BZip2Crc();
        underTestCrc32.Update(new ArraySegment<byte>(longCheck, 15, 18));
        Assert.Equal(0x025846E0L, underTestCrc32.Value);
    }

    [Fact]
    public void CRC_32()
    {
        var underTestCrc32 = new Crc32();
        Assert.Equal(0x0L, underTestCrc32.Value);

        underTestCrc32.Update(check);
        Assert.Equal(0xCBF43926L, underTestCrc32.Value);

        underTestCrc32.Reset();
        Assert.Equal(0x0L, underTestCrc32.Value);

        exceptionTesting(underTestCrc32);
    }

    [Fact]
    public void CRC_32_Long()
    {
        var underTestCrc32 = new Crc32();
        underTestCrc32.Update(longCheck);
        Assert.Equal(0x3E29169CL, underTestCrc32.Value);
    }

    [Fact]
    public void CRC_32_Unaligned()
    {
        // Extract "456" and CRC
        var underTestCrc32 = new Crc32();
        underTestCrc32.Update(new ArraySegment<byte>(check, 3, 3));
        Assert.Equal(0xB1A8C371L, underTestCrc32.Value);
    }

    [Fact]
    public void CRC_32_Long_Unaligned()
    {
        // Extract "789123456789123456" and CRC
        var underTestCrc32 = new Crc32();
        underTestCrc32.Update(new ArraySegment<byte>(longCheck, 15, 18));
        Assert.Equal(0x31CA9A2EL, underTestCrc32.Value);
    }

    private void exceptionTesting(IChecksum crcUnderTest)
    {
        var exception = false;

        try
        {
            crcUnderTest.Update(null);
        }
        catch (ArgumentNullException)
        {
            exception = true;
        }
        Assert.True(exception, "Passing a null buffer should cause an ArgumentNullException");

        // reset exception
        exception = false;
        try
        {
            crcUnderTest.Update(new ArraySegment<byte>(null, 0, 0));
        }
        catch (ArgumentNullException)
        {
            exception = true;
        }
        Assert.True(exception, "Passing a null buffer should cause an ArgumentNullException");

        // reset exception
        exception = false;
        try
        {
            crcUnderTest.Update(new ArraySegment<byte>(check, -1, 9));
        }
        catch (ArgumentOutOfRangeException)
        {
            exception = true;
        }
        Assert.True(exception, "Passing a negative offset should cause an ArgumentOutOfRangeException");

        // reset exception
        exception = false;
        try
        {
            crcUnderTest.Update(new ArraySegment<byte>(check, 10, 0));
        }
        catch (ArgumentException)
        {
            exception = true;
        }
        Assert.True(exception, "Passing an offset greater than buffer.Length should cause an ArgumentException");

        // reset exception
        exception = false;
        try
        {
            crcUnderTest.Update(new ArraySegment<byte>(check, 0, -1));
        }
        catch (ArgumentOutOfRangeException)
        {
            exception = true;
        }
        Assert.True(exception, "Passing a negative count should cause an ArgumentOutOfRangeException");

        // reset exception
        exception = false;
        try
        {
            crcUnderTest.Update(new ArraySegment<byte>(check, 0, 10));
        }
        catch (ArgumentException)
        {
            exception = true;
        }
        Assert.True(exception, "Passing a count + offset greater than buffer.Length should cause an ArgumentException");
    }
}
