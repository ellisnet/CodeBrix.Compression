using CodeBrix.Compression.Lzw;
using CodeBrix.Compression.Tests.TestSupport;
using System.IO;
using Xunit;

namespace CodeBrix.Compression.Tests.Lzw;

[Trait("Category", "LZW")]
public class LzwTestSuite
{
    [Fact]
    public void ZeroLengthInputStream()
    {
        var lis = new LzwInputStream(new MemoryStream());
        var exception = false;
        try
        {
            lis.ReadByte();
        }
        catch
        {
            exception = true;
        }

        Assert.True(exception, "reading from an empty stream should cause an exception");
    }

    [Fact]
    public void InputStreamOwnership()
    {
        var memStream = new TrackedMemoryStream();
        var s = new LzwInputStream(memStream);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.Close();

        Assert.True(memStream.IsClosed, "Should be closed after parent owner close");
        Assert.True(memStream.IsDisposed, "Should be disposed after parent owner close");

        memStream = new TrackedMemoryStream();
        s = new LzwInputStream(memStream);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.IsStreamOwner = false;
        s.Close();

        Assert.False(memStream.IsClosed, "Should not be closed after parent owner close");
        Assert.False(memStream.IsDisposed, "Should not be disposed after parent owner close");
    }
}
