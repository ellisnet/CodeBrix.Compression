using CodeBrix.Compression.Zip;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CodeBrix.Compression.Tests.TestSupport;

/// <summary>
/// Provides support for testing in memory zip archives.
/// </summary>
internal static class ZipTesting
{
    public static void AssertValidZip(Stream stream, string password = null, bool usesAes = true)
    {
        using var zipFile = new ZipFile(stream)
        {
            IsStreamOwner = false,
            Password = password,
        };

        AssertPassesTestArchive(zipFile);

        if (!string.IsNullOrEmpty(password) && usesAes)
        {
            Assert.Skip("ZipInputStream does not support AES");
        }

        stream.Seek(0, SeekOrigin.Begin);

        using var zis = new ZipInputStream(stream) { Password = password };
        while (zis.GetNextEntry() != null)
        {
            new StreamReader(zis).ReadToEnd();
        }
    }

    public static void AssertPassesTestArchive(ZipFile zipFile, string password = null, bool testData = true)
    {
        var report = new TestArchiveReport();
        var passed = zipFile.TestArchive(
            testData,
            TestStrategy.FindAllErrors,
            report.HandleTestResults);
        Assert.True(passed, $"Archive did not pass test: {report}");
    }

    public static void AssertPassesTestArchive(byte[] rawArchive, string password = null, bool testData = true)
    {
        using var ms = new MemoryStream(rawArchive);
        using var zipFile = new ZipFile(ms) { Password = password };
        AssertPassesTestArchive(zipFile, password, testData);
    }
}

public class TestArchiveReport
{
    internal const string PassingArchive = "Passing Archive";

    readonly List<string> _messages = new();
    public void HandleTestResults(TestStatus status, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _messages.Add(message);
    }

    public override string ToString() => _messages.Any() ? string.Join(", ", _messages) : PassingArchive;
}
