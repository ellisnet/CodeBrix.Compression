using System;
using System.IO;
using System.Text;
using CodeBrix.Compression.Core;
using CodeBrix.Compression.Tar;
using CodeBrix.Compression.Tests.TestSupport;
using static CodeBrix.Compression.Tests.TestSupport.Utils;
using Xunit;

namespace CodeBrix.Compression.Tests.Tar;

[Trait("Category", "Tar")]
[Trait("Category", "CreatesTempFile")]
public class TarArchiveTests
{
    [Theory]
    [InlineData("output", false)]
    [InlineData("output/", false)]
    [InlineData(@"output\", true)]
    public void ExtractingContentsWithNonTraversalPathSucceeds(string outputDir, bool winOnly)
    {
        if (winOnly && !OperatingSystem.IsWindows()) Assert.Skip("Windows only");
        var ex = Record.Exception(() => ExtractTarOK(outputDir, "file", allowTraverse: false));
        Assert.Null(ex);
    }

    [Fact]
    public void ExtractingContentsWithExplicitlyAllowedTraversalPathSucceeds()
    {
        var ex = Record.Exception(() => ExtractTarOK("output", "../file", allowTraverse: true));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("output", "../file")]
    [InlineData("output/", "../file")]
    [InlineData("output", "../output.txt")]
    public void ExtractingContentsWithDisallowedPathsFails(string outputDir, string fileName)
    {
        Assert.Throws<InvalidNameException>(() => ExtractTarOK(outputDir, fileName, allowTraverse: false));
    }

    [Theory]
    [InlineData(@"output\", @"..\file")]
    [InlineData(@"output/", @"..\file")]
    [InlineData("output", @"..\output.txt")]
    [InlineData(@"output\", @"..\output.txt")]
    public void ExtractingContentsOnWindowsWithDisallowedPathsFails(string outputDir, string fileName)
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Backslashes are only treated as path separators on windows");
        Assert.Throws<InvalidNameException>(() => ExtractTarOK(outputDir, fileName, allowTraverse: false));
    }

    public void ExtractTarOK(string outputDir, string fileName, bool allowTraverse)
    {
        var fileContent = Encoding.UTF8.GetBytes("file content");
        using var tempDir = GetTempDir();

        var tempPath = tempDir.FullName;
        var extractPath = Path.Combine(tempPath, outputDir);
        var expectedOutputFile = Path.Combine(extractPath, fileName);

        using var archiveStream = new MemoryStream();

        Directory.CreateDirectory(extractPath);

        using (var tos = new TarOutputStream(archiveStream, Encoding.UTF8){IsStreamOwner = false})
        {
            var entry = TarEntry.CreateTarEntry(fileName);
            entry.Size = fileContent.Length;
            tos.PutNextEntry(entry);
            tos.Write(fileContent, 0, fileContent.Length);
            tos.CloseEntry();
        }

        archiveStream.Position = 0;

        using (var ta = TarArchive.CreateInputTarArchive(archiveStream, Encoding.UTF8))
        {
            ta.ProgressMessageEvent += (archive, entry, message)
                => Console.WriteLine($"{entry.Name} {entry.Size} {message}");
            ta.ExtractContents(extractPath, allowTraverse);
        }

        Assert.True(File.Exists(expectedOutputFile));
    }
}
