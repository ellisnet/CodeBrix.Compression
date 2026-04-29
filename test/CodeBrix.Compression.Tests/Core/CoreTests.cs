using CodeBrix.Compression.Core;
using System;
using System.IO;
using Xunit;

namespace CodeBrix.Compression.Tests.Core;

[Trait("Category", "Core")]
public class CoreTestSuite
{
    [Fact]
    public void FilterQuoting()
    {
        var filters = NameFilter.SplitQuoted("");
        Assert.Empty(filters);

        filters = NameFilter.SplitQuoted(";;;");
        Assert.Equal(4, filters.Length);
        foreach (var filter in filters)
        {
            Assert.Equal("", filter);
        }

        filters = NameFilter.SplitQuoted("a;a;a;a;a");
        Assert.Equal(5, filters.Length);
        foreach (var filter in filters)
        {
            Assert.Equal("a", filter);
        }

        filters = NameFilter.SplitQuoted(@"a\;;a\;;a\;;a\;;a\;");
        Assert.Equal(5, filters.Length);
        foreach (var filter in filters)
        {
            Assert.Equal("a;", filter);
        }
    }

    [Fact]
    public void NullFilter()
    {
        var nf = new NameFilter(null);
        Assert.True(nf.IsIncluded("o78i6bgv5rvu\\kj//&*"));
    }

    [Fact]
    public void ValidFilter()
    {
        Assert.True(NameFilter.IsValidFilterExpression(null));
        Assert.True(NameFilter.IsValidFilterExpression(string.Empty));
        Assert.True(NameFilter.IsValidFilterExpression("a"));

        Assert.False(NameFilter.IsValidFilterExpression(@"\,)"));
        Assert.False(NameFilter.IsValidFilterExpression(@"[]"));
    }

    // Use a shorter name wrapper to make tests more legible
    private static string DropRoot(string s) => PathUtils.DropPathRoot(s);

    [Fact]
    public void DropPathRoot_Windows()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows only");
        Assert.Equal("file.txt", DropRoot(@"\\server\share\file.txt"));
        Assert.Equal("file.txt", DropRoot(@"c:\file.txt"));
        Assert.Equal(@"subdir with spaces\file.txt", DropRoot(@"z:\subdir with spaces\file.txt"));
        Assert.Equal("", DropRoot(@"\\server\share\"));
        Assert.Equal(@"server\share\file.txt", DropRoot(@"\server\share\file.txt"));
        Assert.Equal(@"path\file.txt", DropRoot(@"\\server\share\\path\file.txt"));
    }

    [Fact]
    public void DropPathRoot_Posix()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("Posix only");
        Assert.Equal("file.txt", DropRoot("/file.txt"));
        Assert.Equal(@"tmp/file.txt", DropRoot(@"/tmp/file.txt"));
        Assert.Equal(@"tmp\file.txt", DropRoot(@"\tmp\file.txt"));
        Assert.Equal(@"tmp/file.txt", DropRoot(@"\tmp/file.txt"));
        Assert.Equal(@"tmp\file.txt", DropRoot(@"/tmp\file.txt"));
        Assert.Equal("", DropRoot("/"));

    }

    [Theory]
    [InlineData(@"c:\file:+/")]
    [InlineData(@"c:\file*?")]
    [InlineData("c:\\file|\"")]
    [InlineData(@"c:\file<>")]
    [InlineData(@"c:file")]
    [InlineData(@"c::file")]
    [InlineData(@"c:?file")]
    [InlineData(@"c:+file")]
    [InlineData(@"cc:file")]
    public void DropPathRoot_DoesNotThrowForInvalidPath(string path)
    {
        var ex = Record.Exception(() => Console.WriteLine(PathUtils.DropPathRoot(path)));
        Assert.Null(ex);
    }

    [Fact]
    public void GetTempFileName_ReturnsNonExistingPath()
    {
        var tempFileName = PathUtils.GetTempFileName();

        Assert.NotNull(tempFileName);
        Assert.NotEmpty(tempFileName);
        Assert.False(File.Exists(tempFileName), "GetTempFileName should return a path that does not yet exist");
        Assert.Equal(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), Path.GetDirectoryName(tempFileName));
    }

    [Fact]
    public void GetTempFileName_WithOriginal_ReturnsPathBasedOnOriginal()
    {
        var original = Path.Combine(Path.GetTempPath(), "myarchive.zip");
        var tempFileName = PathUtils.GetTempFileName(original);

        Assert.NotNull(tempFileName);
        Assert.NotEmpty(tempFileName);
        Assert.StartsWith(original + ".", tempFileName);
        Assert.False(File.Exists(tempFileName));
    }

    [Fact]
    public void GetTempFileName_ReturnsUniqueValues()
    {
        var first = PathUtils.GetTempFileName();
        var second = PathUtils.GetTempFileName();

        Assert.NotEqual(second, first);
    }
}
