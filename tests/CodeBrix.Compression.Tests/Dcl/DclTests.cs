using CodeBrix.Compression.Dcl;
using CodeBrix.Compression.Tests.TestSupport;
using System.IO;
using System.Text;
using Xunit;
using DclHelper = CodeBrix.Compression.Dcl.Dcl;

namespace CodeBrix.Compression.Tests.Dcl;

[Trait("Category", "DCL")]
public class DclTestSuite
{
    // The canonical DCL "implode" reference vector from Mark Adler's blast.c,
    // originally from Ben Rudiak-Gould's format description in comp.compression:
    // decompresses to "AIAIAIAIAIAIA". Exercises uncoded literals, a 1024-byte
    // dictionary header, and an overlapped length/distance copy.
    private static readonly byte[] ReferenceCompressed =
        { 0x00, 0x04, 0x82, 0x24, 0x25, 0x8f, 0x80, 0x7f };

    private const string ReferenceExpected = "AIAIAIAIAIAIA";

    [Fact]
    public void DecompressReferenceVector()
    {
        //Arrange
        using var inputStream = new DclInputStream(new MemoryStream(ReferenceCompressed));
        using var outputStream = new MemoryStream();

        //Act
        inputStream.CopyTo(outputStream);

        //Assert
        Assert.Equal(ReferenceExpected, Encoding.ASCII.GetString(outputStream.ToArray()));
    }

    [Fact]
    public void DecompressReferenceVectorWithSingleByteReads()
    {
        //Arrange
        using var inputStream = new DclInputStream(new MemoryStream(ReferenceCompressed));
        using var outputStream = new MemoryStream();

        //Act
        int value;
        while ((value = inputStream.ReadByte()) >= 0)
        {
            outputStream.WriteByte((byte)value);
        }

        //Assert
        Assert.Equal(ReferenceExpected, Encoding.ASCII.GetString(outputStream.ToArray()));
    }

    [Fact]
    public void DecompressReferenceVectorWithStaticHelper()
    {
        //Arrange
        var inputStream = new MemoryStream(ReferenceCompressed);
        var outputStream = new MemoryStream();

        //Act
        DclHelper.Decompress(inputStream, outputStream, isStreamOwner: false);

        //Assert
        Assert.Equal(ReferenceExpected, Encoding.ASCII.GetString(outputStream.ToArray()));
    }

    [Fact]
    public void ReadAfterEndOfStreamReturnsZero()
    {
        //Arrange
        using var inputStream = new DclInputStream(new MemoryStream(ReferenceCompressed));
        var buffer = new byte[64];
        while (inputStream.Read(buffer, 0, buffer.Length) > 0)
        {
        }

        //Act
        var bytesRead = inputStream.Read(buffer, 0, buffer.Length);

        //Assert
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void ZeroLengthInputStream()
    {
        //Arrange
        using var inputStream = new DclInputStream(new MemoryStream());

        //Act + Assert
        Assert.Throws<DclException>(() => inputStream.ReadByte());
    }

    [Fact]
    public void InvalidLiteralModeFlagThrows()
    {
        //Arrange - first header byte must be 0 or 1
        using var inputStream = new DclInputStream(new MemoryStream(new byte[] { 0x02, 0x04, 0x00 }));

        //Act + Assert
        Assert.Throws<DclException>(() => inputStream.ReadByte());
    }

    [Theory]
    [InlineData(0x03)]
    [InlineData(0x07)]
    public void InvalidDictionarySizeThrows(byte dictionarySizeCode)
    {
        //Arrange - second header byte must be 4, 5 or 6
        using var inputStream = new DclInputStream(new MemoryStream(new byte[] { 0x00, dictionarySizeCode, 0x00 }));

        //Act + Assert
        Assert.Throws<DclException>(() => inputStream.ReadByte());
    }

    [Fact]
    public void TruncatedStreamThrows()
    {
        //Arrange - the reference vector with its final two bytes (holding the
        //end-of-stream code) removed
        var truncated = new byte[ReferenceCompressed.Length - 2];
        System.Array.Copy(ReferenceCompressed, truncated, truncated.Length);
        using var inputStream = new DclInputStream(new MemoryStream(truncated));
        using var outputStream = new MemoryStream();

        //Act + Assert
        Assert.Throws<DclException>(() => inputStream.CopyTo(outputStream));
    }

    [Fact]
    public void DistanceBeforeStartOfOutputThrows()
    {
        //Arrange - a hand-built stream whose first token is a length/distance
        //copy (length 3, distance 1) before any literal has been output
        using var inputStream = new DclInputStream(new MemoryStream(new byte[] { 0x00, 0x04, 0x1f, 0x00 }));

        //Act + Assert
        Assert.Throws<DclException>(() => inputStream.ReadByte());
    }

    [Fact]
    public void InputStreamOwnership()
    {
        var memStream = new TrackedMemoryStream();
        var s = new DclInputStream(memStream);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.Close();

        Assert.True(memStream.IsClosed, "Should be closed after parent owner close");
        Assert.True(memStream.IsDisposed, "Should be disposed after parent owner close");

        memStream = new TrackedMemoryStream();
        s = new DclInputStream(memStream);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.IsStreamOwner = false;
        s.Close();

        Assert.False(memStream.IsClosed, "Should not be closed after parent owner close");
        Assert.False(memStream.IsDisposed, "Should not be disposed after parent owner close");
    }
}
