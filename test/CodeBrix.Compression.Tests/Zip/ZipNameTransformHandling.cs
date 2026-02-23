using CodeBrix.Compression.Core;
using CodeBrix.Compression.Tests.TestSupport;
using CodeBrix.Compression.Zip;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;

namespace CodeBrix.Compression.Tests.Zip;

[TestFixture]
public class ZipNameTransformHandling : TransformBase
{
    [Test]
    [Category("Zip")]
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

    [Test]
    [Category("Zip")]
    [Platform("Win")]
    public void Basic_Windows()
    {
        var t = new ZipNameTransform();
        TestFile(t, @"\\uncpath\d1\file1", "file1");
        TestFile(t, @"C:\absolute\file2", "absolute/file2");
			
        TestFile(t, @"c::file", "_file");
    }
		
    [Test]
    [Category("Zip")]
    [Platform(Exclude="Win")]
    public void Basic_Posix()
    {
        var t = new ZipNameTransform();
        TestFile(t, @"backslash_path\file1", "backslash_path/file1");
        TestFile(t, "/absolute/file2", "absolute/file2");
			
        TestFile(t, @"////////:file", "_file");
    }

    [Test]
    public void TooLong()
    {
        var zt = new ZipNameTransform();
        var tooLong = new string('x', 65536);
        Assert.Throws<PathTooLongException>(() => zt.TransformDirectory(tooLong));
    }

    [Test]
    public void LengthBoundaryOk()
    {
        var zt = new ZipNameTransform();
        var tooLongWithRoot = Utils.SystemRoot + new string('x', 65535);
        Assert.DoesNotThrow(() => zt.TransformDirectory(tooLongWithRoot));
    }

    [Test]
    [Category("Zip")]
    [Platform("Win")]
    public void NameTransforms_Windows()
    {
        INameTransform t = new ZipNameTransform(@"C:\Slippery");
        ClassicAssert.AreEqual("Pongo/Directory/", t.TransformDirectory(@"C:\Slippery\Pongo\Directory"), "Value should be trimmed and converted");
        ClassicAssert.AreEqual("PoNgo/Directory/", t.TransformDirectory(@"c:\slipperY\PoNgo\Directory"), "Trimming should be case insensitive");
        ClassicAssert.AreEqual("slippery/Pongo/Directory/", t.TransformDirectory(@"d:\slippery\Pongo\Directory"), "Trimming should account for root");

        ClassicAssert.AreEqual("Pongo/File", t.TransformFile(@"C:\Slippery\Pongo\File"), "Value should be trimmed and converted");
    }
		
    [Test]
    [Category("Zip")]
    [Platform(Exclude="Win")]
    public void NameTransforms_Posix()
    {
        INameTransform t = new ZipNameTransform(@"/Slippery");
        ClassicAssert.AreEqual("Pongo/Directory/", t.TransformDirectory(@"/Slippery\Pongo\Directory"), "Value should be trimmed and converted");
        ClassicAssert.AreEqual("PoNgo/Directory/", t.TransformDirectory(@"/slipperY\PoNgo\Directory"), "Trimming should be case insensitive");
        ClassicAssert.AreEqual("slippery/Pongo/Directory/", t.TransformDirectory(@"/slippery/slippery/Pongo/Directory"), "Trimming should account for root");

        ClassicAssert.AreEqual("Pongo/File", t.TransformFile(@"/Slippery/Pongo/File"), "Value should be trimmed and converted");
    }

    /// <summary>
    /// Test ZipEntry static file name cleaning methods
    /// </summary>
    [Test]
    [Category("Zip")]
    public void FilenameCleaning()
    {
        ClassicAssert.AreEqual("hello", ZipEntry.CleanName("hello"));
        if(Environment.OSVersion.Platform == PlatformID.Win32NT) 
        {
            ClassicAssert.AreEqual("eccles", ZipEntry.CleanName(@"z:\eccles"));
            ClassicAssert.AreEqual("eccles", ZipEntry.CleanName(@"\\server\share\eccles"));
            ClassicAssert.AreEqual("dir/eccles", ZipEntry.CleanName(@"\\server\share\dir\eccles"));
        }
        else {
            ClassicAssert.AreEqual("eccles", ZipEntry.CleanName(@"/eccles"));
        }
    }

    [Test]
    [Category("Zip")]
    public void PathalogicalNames()
    {
        var badName = ".*:\\zy3$";

        ClassicAssert.IsFalse(ZipNameTransform.IsValidName(badName));

        var t = new ZipNameTransform();
        var result = t.TransformFile(badName);

        ClassicAssert.IsTrue(ZipNameTransform.IsValidName(result));
    }
}