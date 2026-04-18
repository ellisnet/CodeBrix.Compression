using CodeBrix.Compression.Core;
using CodeBrix.Compression.Zip;
using System;
using System.IO;
using Xunit;

namespace CodeBrix.Compression.Tests.Zip;

public class WindowsNameTransformHandling : TransformBase
{
    public WindowsNameTransformHandling()
    {
        if (Path.DirectorySeparatorChar != '\\') {
            Assert.Skip("WindowsNameTransform will not work on platforms not using '\\' directory separators");
        }
    }

    [Fact]
    public void BasicFiles()
    {
        var wnt = new WindowsNameTransform();
        wnt.TrimIncomingPaths = false;

        TestFile(wnt, "Bogan", "Bogan");
        TestFile(wnt, "absolute/file2", Path.Combine("absolute", "file2"));
        TestFile(wnt, "C:/base/////////t", Path.Combine("base", "t"));
        TestFile(wnt, "//unc/share/zebidi/and/dylan", Path.Combine("zebidi", "and", "dylan"));
        TestFile(wnt, @"\\unc\share\/zebidi\/and\/dylan", Path.Combine("zebidi", "and", "dylan"));
    }

    [Fact]
    public void Replacement()
    {
        var wnt = new WindowsNameTransform();
        wnt.TrimIncomingPaths = false;

        TestFile(wnt, "c::", "_");
        TestFile(wnt, "c\\/>", Path.Combine("c", "_"));
    }

    [Fact]
    public void NameTooLong()
    {
        var wnt = new WindowsNameTransform();
        var veryLong = new string('x', 261);
        try
        {
            wnt.TransformDirectory(veryLong);
            Assert.Fail("Expected an exception");
        }
        catch (PathTooLongException)
        {
        }
    }

    [Fact]
    public void LengthBoundaryOk()
    {
        var wnt = new WindowsNameTransform();
        var veryLong = "c:\\" + new string('x', 260);
        try
        {
            var transformed = wnt.TransformDirectory(veryLong);
        }
        catch
        {
            Assert.Fail("Expected no exception");
        }
    }

    [Fact]
    public void ReplacementChecking()
    {
        var wnt = new WindowsNameTransform();
        try
        {
            wnt.Replacement = '*';
            Assert.Fail("Expected an exception");
        }
        catch (ArgumentException)
        {
        }

        try
        {
            wnt.Replacement = '?';
            Assert.Fail("Expected an exception");
        }
        catch (ArgumentException)
        {
        }

        try
        {
            wnt.Replacement = ':';
            Assert.Fail("Expected an exception");
        }
        catch (ArgumentException)
        {
        }

        try
        {
            wnt.Replacement = '/';
            Assert.Fail("Expected an exception");
        }
        catch (ArgumentException)
        {
        }

        try
        {
            wnt.Replacement = '\\';
            Assert.Fail("Expected an exception");
        }
        catch (ArgumentException)
        {
        }
    }

    [Fact]
    public void BasicDirectories()
    {
        var wnt = new WindowsNameTransform();
        wnt.TrimIncomingPaths = false;

        var tutu = Path.GetDirectoryName("\\bogan\\ping.txt");
        TestDirectory(wnt, "d/", "d");
        TestDirectory(wnt, "d", "d");
        TestDirectory(wnt, "absolute/file2", @"absolute\file2");

        var BaseDir1 = Path.Combine("C:\\", "Dir");
        wnt.BaseDirectory = BaseDir1;

        TestDirectory(wnt, "talofa", Path.Combine(BaseDir1, "talofa"));

        var BaseDir2 = string.Format(@"C:{0}Dir{0}", Path.DirectorySeparatorChar);
        wnt.BaseDirectory = BaseDir2;

        TestDirectory(wnt, "talofa", Path.Combine(BaseDir2, "talofa"));
    }

    [Fact]
    public void ParentTraversalBlockedByDefault()
    {
        var baseDir = Path.Combine("C:\\", "ExtractDir");
        var wnt = new WindowsNameTransform(baseDir);

        Assert.Throws<InvalidNameException>(() => wnt.TransformFile("../escape.txt"));
        Assert.Throws<InvalidNameException>(() => wnt.TransformFile("..\\escape.txt"));
        Assert.Throws<InvalidNameException>(() => wnt.TransformFile("subdir/../../escape.txt"));
    }

    [Fact]
    public void ParentTraversalAllowedWhenExplicitlyEnabled()
    {
        var baseDir = Path.Combine("C:\\", "ExtractDir");
        var wnt = new WindowsNameTransform(baseDir, allowParentTraversal: true);

        var ex1 = Record.Exception(() => wnt.TransformFile("../escape.txt"));
        Assert.Null(ex1);
        var ex2 = Record.Exception(() => wnt.TransformFile("subdir/../../escape.txt"));
        Assert.Null(ex2);
    }
}
