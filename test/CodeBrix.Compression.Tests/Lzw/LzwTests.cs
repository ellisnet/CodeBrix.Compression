using CodeBrix.Compression.Lzw;
using CodeBrix.Compression.Tests.TestSupport;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.IO;

namespace CodeBrix.Compression.Tests.Lzw;

[TestFixture]
public class LzwTestSuite
{
    [Test]
    [Category("LZW")]
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

        ClassicAssert.IsTrue(exception, "reading from an empty stream should cause an exception");
    }

    [Test]
    [Category("LZW")]
    public void InputStreamOwnership()
    {
        var memStream = new TrackedMemoryStream();
        var s = new LzwInputStream(memStream);

        ClassicAssert.IsFalse(memStream.IsClosed, "Shouldnt be closed initially");
        ClassicAssert.IsFalse(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.Close();

        ClassicAssert.IsTrue(memStream.IsClosed, "Should be closed after parent owner close");
        ClassicAssert.IsTrue(memStream.IsDisposed, "Should be disposed after parent owner close");

        memStream = new TrackedMemoryStream();
        s = new LzwInputStream(memStream);

        ClassicAssert.IsFalse(memStream.IsClosed, "Shouldnt be closed initially");
        ClassicAssert.IsFalse(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.IsStreamOwner = false;
        s.Close();

        ClassicAssert.IsFalse(memStream.IsClosed, "Should not be closed after parent owner close");
        ClassicAssert.IsFalse(memStream.IsDisposed, "Should not be disposed after parent owner close");
    }
}