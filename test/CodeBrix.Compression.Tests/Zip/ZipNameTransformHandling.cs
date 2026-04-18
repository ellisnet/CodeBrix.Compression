using CodeBrix.Compression.Core;
using CodeBrix.Compression.Tests.TestSupport;
using CodeBrix.Compression.Zip;
using System;
using System.IO;
using Xunit;

namespace CodeBrix.Compression.Tests.Zip;

[Trait("Category", "Zip")]
public class ZipNameTransformHandling : TransformBase
{
    [Fact]
    public void Basic()
    {
        var t = new ZipNameTransform();

        TestFile(t, "abcdef", "abcdef");

        // This is ignored but could be converted to 'file3'
        TestFile(t, @"./file3", "./file3");

        // The following relative paths cant be handled and are ignored
        TestFile(t, @"../file3", "../file3");
        TestFile(t, @".../file3", ".../file3");

        // Trick filenames.
        TestFile(t, @".....file3", ".....file3");
    }

    [Fact]
    public void Basic_Windows()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows only");
        var t = new ZipNameTransform();
        TestFile(t, @"\\uncpath\d1\file1", "file1");
        TestFile(t, @"C:\absolute\file2", "absolute/file2");

        TestFile(t, @"c::file", "_file");
    }

    [Fact]
    public void Basic_Posix()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("Posix only");
        var t = new ZipNameTransform();
        TestFile(t, @"backslash_path\file1", "backslash_path/file1");
        TestFile(t, "/absolute/file2", "absolute/file2");

        TestFile(t, @"////////:file", "_file");
    }

    [Fact]
    public void TooLong()
    {
        var zt = new ZipNameTransform();
        var tooLong = new string('x', 65536);
        Assert.Throws<PathTooLongException>(() => zt.TransformDirectory(tooLong));
    }

    [Fact]
    public void LengthBoundaryOk()
    {
        var zt = new ZipNameTransform();
        var tooLongWithRoot = Utils.SystemRoot + new string('x', 65535);
        var ex = Record.Exception(() => zt.TransformDirectory(tooLongWithRoot));
        Assert.Null(ex);
    }

    [Fact]
    public void NameTransforms_Windows()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows only");
        INameTransform t = new ZipNameTransform(@"C:\Slippery");
        Assert.Equal("Pongo/Directory/", t.TransformDirectory(@"C:\Slippery\Pongo\Directory"));
        Assert.Equal("PoNgo/Directory/", t.TransformDirectory(@"c:\slipperY\PoNgo\Directory"));
        Assert.Equal("slippery/Pongo/Directory/", t.TransformDirectory(@"d:\slippery\Pongo\Directory"));

        Assert.Equal("Pongo/File", t.TransformFile(@"C:\Slippery\Pongo\File"));
    }

    [Fact]
    public void NameTransforms_Posix()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("Posix only");
        INameTransform t = new ZipNameTransform(@"/Slippery");
        Assert.Equal("Pongo/Directory/", t.TransformDirectory(@"/Slippery\Pongo\Directory"));
        Assert.Equal("PoNgo/Directory/", t.TransformDirectory(@"/slipperY\PoNgo\Directory"));
        Assert.Equal("slippery/Pongo/Directory/", t.TransformDirectory(@"/slippery/slippery/Pongo/Directory"));

        Assert.Equal("Pongo/File", t.TransformFile(@"/Slippery/Pongo/File"));
    }

    /// <summary>
    /// Test ZipEntry static file name cleaning methods
    /// </summary>
    [Fact]
    public void FilenameCleaning()
    {
        Assert.Equal("hello", ZipEntry.CleanName("hello"));
        if(Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            Assert.Equal("eccles", ZipEntry.CleanName(@"z:\eccles"));
            Assert.Equal("eccles", ZipEntry.CleanName(@"\\server\share\eccles"));
            Assert.Equal("dir/eccles", ZipEntry.CleanName(@"\\server\share\dir\eccles"));
        }
        else {
            Assert.Equal("eccles", ZipEntry.CleanName(@"/eccles"));
        }
    }

    [Fact]
    public void PathalogicalNames()
    {
        var badName = ".*:\\zy3$";

        Assert.False(ZipNameTransform.IsValidName(badName));

        var t = new ZipNameTransform();
        var result = t.TransformFile(badName);

        Assert.True(ZipNameTransform.IsValidName(result));
    }
}
