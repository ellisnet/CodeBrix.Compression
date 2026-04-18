using CodeBrix.Compression.Tests.TestSupport;
using CodeBrix.Compression.Tests.Zip;
using CodeBrix.Compression.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

// As there is no way to order the test namespace execution order we use a name that should be alphabetically sorted before any other namespace
// This is because we have one test that only works when no encoding provider has been loaded which is not reversable once done.
namespace CodeBrix.Compression.Tests._Zip;

public class ZipStringsTests
{
    [Fact]
    // NOTE: This test needs to be run before any test registering CodePagesEncodingProvider.Instance
    public void TestSystemDefaultEncoding()
    {
        Console.WriteLine($"Default encoding before registering provider: {Encoding.GetEncoding(0).EncodingName}");
        Encoding.RegisterProvider(new TestEncodingProvider());
        Console.WriteLine($"Default encoding after registering provider: {Encoding.GetEncoding(0).EncodingName}");

        // Initialize a default StringCodec
        var sc = StringCodec.Default;

        var legacyEncoding = sc.ZipEncoding(false);
        Assert.Equal(TestEncodingProvider.DefaultEncodingName, legacyEncoding.EncodingName);
        Assert.Equal(TestEncodingProvider.DefaultEncodingCodePage, legacyEncoding.CodePage);
    }

    [Fact]
    public void TestFastZipRoundTripWithCodePage()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var ms = new MemoryStream();
        using var zipFile = new TempFile();
        using var srcDir = new TempDir();
        using var dstDir = new TempDir();

        srcDir.CreateDummyFile("file1");
        srcDir.CreateDummyFile("слово");

        foreach(var f in Directory.EnumerateFiles(srcDir.FullName))
        {
            Console.WriteLine(f);
        }

        var fzCreate = new FastZip() { StringCodec = StringCodec.FromCodePage(866), UseUnicode = false };
        fzCreate.CreateZip(zipFile, srcDir.FullName, true, null);

        var fzExtract = new FastZip() { StringCodec = StringCodec.FromCodePage(866) };
        fzExtract.ExtractZip(zipFile, dstDir.FullName, null);

        foreach (var f in Directory.EnumerateFiles(dstDir.FullName))
        {
            Console.WriteLine(f);
        }

        Assert.True(File.Exists(dstDir.GetFile("file1").FullName) || Directory.Exists(dstDir.GetFile("file1").FullName));
        Assert.True(File.Exists(dstDir.GetFile("слово").FullName) || Directory.Exists(dstDir.GetFile("слово").FullName));
    }


    [Fact]
    public void TestZipFileRoundTripWithCodePage()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var ms = new MemoryStream();
        using (var zf = ZipFile.Create(ms))
        {
            zf.StringCodec = StringCodec.FromCodePage(866);
            zf.BeginUpdate();
            zf.Add(MemoryDataSource.Empty, "file1", CompressionMethod.Stored, useUnicodeText: false);
            zf.Add(MemoryDataSource.Empty, "слово", CompressionMethod.Stored, useUnicodeText: false);
            zf.CommitUpdate();
        }

        ms.Seek(0, SeekOrigin.Begin);

        using (var zf = new ZipFile(ms, false, StringCodec.FromCodePage(866)) { IsStreamOwner = false })
        {
            Assert.NotNull(zf.GetEntry("file1"));
            Assert.NotNull(zf.GetEntry("слово"));
        }

    }

    [Fact]
    public void TestZipStreamRoundTripWithCodePage()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var ms = new MemoryStream();
        using (var zos = new ZipOutputStream(ms, StringCodec.FromCodePage(866)) { IsStreamOwner = false })
        {
            zos.PutNextEntry(new ZipEntry("file1") { IsUnicodeText = false });
            zos.PutNextEntry(new ZipEntry("слово") { IsUnicodeText = false });
        }

        ms.Seek(0, SeekOrigin.Begin);

        using (var zis = new ZipInputStream(ms, StringCodec.FromCodePage(866)) { IsStreamOwner = false })
        {
            Assert.Equal("file1", zis.GetNextEntry().Name);
            Assert.Equal("слово", zis.GetNextEntry().Name);
        }

    }

    [Fact]
    public void TestZipCryptoPasswordEncodingRoundtrip()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var content = Utils.GetDummyBytes(32);

        using var ms = new MemoryStream();
        using (var zos = new ZipOutputStream(ms, StringCodec.FromCodePage(866)) { IsStreamOwner = false })
        {
            zos.Password = "слово";
            zos.PutNextEntry(new ZipEntry("file1"));
            zos.Write(content, 0, content.Length);
        }

        ms.Seek(0, SeekOrigin.Begin);

        using (var zis = new ZipInputStream(ms, StringCodec.FromCodePage(866)) { IsStreamOwner = false })
        {
            zis.Password = "слово";
            var entry = zis.GetNextEntry();
            var output = new byte[32];
            Assert.Equal(32, zis.Read(output, 0, 32));
            Assert.Equal(content, output);
        }

    }

    [Fact]
    public void TestZipStreamCommentEncodingRoundtrip()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var content = Utils.GetDummyBytes(32);

        using var ms = new MemoryStream();
        using (var zos = new ZipOutputStream(ms, StringCodec.FromCodePage(866)) { IsStreamOwner = false })
        {
            zos.SetComment("слово");
        }

        ms.Seek(0, SeekOrigin.Begin);

        using var zf = new ZipFile(ms, false, StringCodec.FromCodePage(866));
        Assert.Equal("слово", zf.ZipFileComment);
    }


    [Fact]
    public void TestZipFileCommentEncodingRoundtrip()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var content = Utils.GetDummyBytes(32);

        using var ms = new MemoryStream();
        using (var zf = ZipFile.Create(ms))
        {
            zf.StringCodec = StringCodec.FromCodePage(866);
            zf.BeginUpdate();
            zf.SetComment("слово");
            zf.CommitUpdate();
        }

        ms.Seek(0, SeekOrigin.Begin);

        using (var zf = new ZipFile(ms, false, StringCodec.FromCodePage(866)))
        {
            Assert.Equal("слово", zf.ZipFileComment);
        }
    }
}


internal class TestEncodingProvider : EncodingProvider
{
    internal static string DefaultEncodingName = "TestDefaultEncoding";
    internal static int DefaultEncodingCodePage = -37;

    class TestDefaultEncoding : Encoding
    {
        public override string EncodingName => DefaultEncodingName;
        public override int CodePage => DefaultEncodingCodePage;

        public override int GetByteCount(char[] chars, int index, int count)
            => UTF8.GetByteCount(chars, index, count);

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
            => UTF8.GetBytes(chars, charIndex, charCount, bytes, byteIndex);

        public override int GetCharCount(byte[] bytes, int index, int count)
            => UTF8.GetCharCount(bytes, index, count);

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
            => UTF8.GetChars(bytes, byteIndex, byteCount, chars, charIndex);

        public override int GetMaxByteCount(int charCount) => UTF8.GetMaxByteCount(charCount);

        public override int GetMaxCharCount(int byteCount) => UTF8.GetMaxCharCount(byteCount);
    }

    TestDefaultEncoding testDefaultEncoding = new();

    public override Encoding GetEncoding(int codepage)
        => (codepage == 0 || codepage == DefaultEncodingCodePage) ? testDefaultEncoding : null;

    public override Encoding GetEncoding(string name)
        => DefaultEncodingName == name ? testDefaultEncoding : null;

    public override IEnumerable<EncodingInfo> GetEncodings()
    {
        yield return new EncodingInfo(this, DefaultEncodingCodePage, DefaultEncodingName, DefaultEncodingName);
    }
}
