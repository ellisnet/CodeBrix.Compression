using CodeBrix.Compression.Core;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;

namespace CodeBrix.Compression.Tests.Core;

[TestFixture]
public class CoreTestSuite
{
    [Test]
    [Category("Core")]
    public void FilterQuoting()
    {
        var filters = NameFilter.SplitQuoted("");
        ClassicAssert.AreEqual(0, filters.Length);

        filters = NameFilter.SplitQuoted(";;;");
        ClassicAssert.AreEqual(4, filters.Length);
        foreach (var filter in filters)
        {
            ClassicAssert.AreEqual("", filter);
        }

        filters = NameFilter.SplitQuoted("a;a;a;a;a");
        ClassicAssert.AreEqual(5, filters.Length);
        foreach (var filter in filters)
        {
            ClassicAssert.AreEqual("a", filter);
        }

        filters = NameFilter.SplitQuoted(@"a\;;a\;;a\;;a\;;a\;");
        ClassicAssert.AreEqual(5, filters.Length);
        foreach (var filter in filters)
        {
            ClassicAssert.AreEqual("a;", filter);
        }
    }

    [Test]
    [Category("Core")]
    public void NullFilter()
    {
        var nf = new NameFilter(null);
        ClassicAssert.IsTrue(nf.IsIncluded("o78i6bgv5rvu\\kj//&*"));
    }

    [Test]
    [Category("Core")]
    public void ValidFilter()
    {
        ClassicAssert.IsTrue(NameFilter.IsValidFilterExpression(null));
        ClassicAssert.IsTrue(NameFilter.IsValidFilterExpression(string.Empty));
        ClassicAssert.IsTrue(NameFilter.IsValidFilterExpression("a"));

        ClassicAssert.IsFalse(NameFilter.IsValidFilterExpression(@"\,)"));
        ClassicAssert.IsFalse(NameFilter.IsValidFilterExpression(@"[]"));
    }

    // Use a shorter name wrapper to make tests more legible
    private static string DropRoot(string s) => PathUtils.DropPathRoot(s);
		
    [Test]
    [Category("Core")]
    [Platform("Win")]
    public void DropPathRoot_Windows()
    {
        ClassicAssert.AreEqual("file.txt", DropRoot(@"\\server\share\file.txt"));
        ClassicAssert.AreEqual("file.txt", DropRoot(@"c:\file.txt"));
        ClassicAssert.AreEqual(@"subdir with spaces\file.txt", DropRoot(@"z:\subdir with spaces\file.txt"));
        ClassicAssert.AreEqual("", DropRoot(@"\\server\share\"));
        ClassicAssert.AreEqual(@"server\share\file.txt", DropRoot(@"\server\share\file.txt"));
        ClassicAssert.AreEqual(@"path\file.txt", DropRoot(@"\\server\share\\path\file.txt"));
    }

    [Test]
    [Category("Core")]
    [Platform(Exclude="Win")]
    public void DropPathRoot_Posix()
    {
        ClassicAssert.AreEqual("file.txt", DropRoot("/file.txt"));
        ClassicAssert.AreEqual(@"tmp/file.txt", DropRoot(@"/tmp/file.txt"));
        ClassicAssert.AreEqual(@"tmp\file.txt", DropRoot(@"\tmp\file.txt"));
        ClassicAssert.AreEqual(@"tmp/file.txt", DropRoot(@"\tmp/file.txt"));
        ClassicAssert.AreEqual(@"tmp\file.txt", DropRoot(@"/tmp\file.txt"));
        ClassicAssert.AreEqual("", DropRoot("/"));

    }

    [Test]
    [TestCase(@"c:\file:+/")]
    [TestCase(@"c:\file*?")]
    [TestCase("c:\\file|\"")]
    [TestCase(@"c:\file<>")]
    [TestCase(@"c:file")]
    [TestCase(@"c::file")]
    [TestCase(@"c:?file")]
    [TestCase(@"c:+file")]
    [TestCase(@"cc:file")]
    [Category("Core")]
    public void DropPathRoot_DoesNotThrowForInvalidPath(string path)
    {
        Assert.DoesNotThrow(() => Console.WriteLine(PathUtils.DropPathRoot(path)));
    }

    [Test]
    [Category("Core")]
    public void GetTempFileName_ReturnsNonExistingPath()
    {
        var tempFileName = PathUtils.GetTempFileName();

        Assert.That(tempFileName, Is.Not.Null.And.Not.Empty);
        Assert.That(File.Exists(tempFileName), Is.False, "GetTempFileName should return a path that does not yet exist");
        Assert.That(Path.GetDirectoryName(tempFileName), Is.EqualTo(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)));
    }

    [Test]
    [Category("Core")]
    public void GetTempFileName_WithOriginal_ReturnsPathBasedOnOriginal()
    {
        var original = Path.Combine(Path.GetTempPath(), "myarchive.zip");
        var tempFileName = PathUtils.GetTempFileName(original);

        Assert.That(tempFileName, Is.Not.Null.And.Not.Empty);
        Assert.That(tempFileName, Does.StartWith(original + "."));
        Assert.That(File.Exists(tempFileName), Is.False);
    }

    [Test]
    [Category("Core")]
    public void GetTempFileName_ReturnsUniqueValues()
    {
        var first = PathUtils.GetTempFileName();
        var second = PathUtils.GetTempFileName();

        Assert.That(first, Is.Not.EqualTo(second), "Successive calls should return different paths");
    }
}