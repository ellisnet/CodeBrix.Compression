using CodeBrix.Compression.GZip;
using CodeBrix.Compression.Tar;
using CodeBrix.Compression.Tests.TestSupport;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.Compression.Tests.Tar;

/// <summary>
/// This class contains test cases for Tar archive handling.
/// </summary>
[Trait("Category", "Tar")]
public class TarTestSuite
{
    private int entryCount;

    private void EntryCounter(TarArchive archive, TarEntry entry, string message)
    {
        entryCount++;
    }

    public TarTestSuite()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Test that an empty archive can be created and when read has 0 entries in it
    /// </summary>
    [Fact]
    public void EmptyTar()
    {
        var ms = new MemoryStream();
        int recordSize;
        using (var tarOut = TarArchive.CreateOutputTarArchive(ms))
        {
            recordSize = tarOut.RecordSize;
        }

        Assert.True(ms.GetBuffer().Length > 0, "Archive size must be > zero");
        Assert.Equal(0, ms.GetBuffer().Length % recordSize);

        var ms2 = new MemoryStream();
        ms2.Write(ms.GetBuffer(), 0, ms.GetBuffer().Length);
        ms2.Seek(0, SeekOrigin.Begin);

        using (var tarIn = TarArchive.CreateInputTarArchive(ms2, nameEncoding: null))
        {
            entryCount = 0;
            tarIn.ProgressMessageEvent += EntryCounter;
            tarIn.ListContents();
            Assert.Equal(0, entryCount);
        }
    }

    /// <summary>
    /// Check that the tar block factor can be varied successfully.
    /// </summary>
    [Fact]
    public void BlockFactorHandling()
    {
        const int minimumBlockFactor = 1;
        const int maximumBlockFactor = 64;
        const int fillFactor = 2;

        for (var factor = minimumBlockFactor; factor < maximumBlockFactor; ++factor)
        {
            var ms = new MemoryStream();

            using (var tarOut = new TarOutputStream(ms, factor, nameEncoding: null))
            {
                var entry = TarEntry.CreateTarEntry("TestEntry");
                entry.Size = TarBuffer.BlockSize * factor * fillFactor;
                tarOut.PutNextEntry(entry);

                var buffer = Utils.GetDummyBytes(TarBuffer.BlockSize);

                // Last block is a partial one
                for (var i = 0; i < factor * fillFactor; ++i)
                {
                    tarOut.Write(buffer, 0, buffer.Length);
                }
            }

            var tarData = ms.ToArray();
            Assert.NotNull(tarData);

            // Blocks = Header + Data Blocks + Zero block + Record trailer
            var usedBlocks = 1 + (factor * fillFactor) + 2;
            var totalBlocks = usedBlocks + (factor - 1);
            totalBlocks /= factor;
            totalBlocks *= factor;

            Assert.Equal(TarBuffer.BlockSize * totalBlocks, tarData.Length);

            if (usedBlocks >= totalBlocks)
            {
                continue;
            }

            // Start at first byte after header.
            var byteIndex = TarBuffer.BlockSize * ((factor * fillFactor) + 1);
            while (byteIndex < tarData.Length)
            {
                var blockNumber = byteIndex / TarBuffer.BlockSize;
                var offset = blockNumber % TarBuffer.BlockSize;
                Assert.True(0 == tarData[byteIndex],
                    $"Trailing block data should be null iteration {factor} block {blockNumber} offset {offset}  index {byteIndex}");
                byteIndex += 1;
            }
        }
    }

    /// <summary>
    /// Check that the tar trailer only contains nulls.
    /// </summary>
    [Fact]
    public void TrailerContainsNulls()
    {
        const int testBlockFactor = 3;

        for (var iteration = 0; iteration < testBlockFactor * 2; ++iteration)
        {
            var ms = new MemoryStream();

            using (var tarOut = new TarOutputStream(ms, testBlockFactor, null))
            {
                var entry = TarEntry.CreateTarEntry("TestEntry");
                if (iteration > 0)
                {
                    entry.Size = (TarBuffer.BlockSize * (iteration - 1)) + 9;
                }
                tarOut.PutNextEntry(entry);

                var buffer = Utils.GetDummyBytes(TarBuffer.BlockSize);

                if (iteration > 0)
                {
                    for (var i = 0; i < iteration - 1; ++i)
                    {
                        tarOut.Write(buffer, 0, buffer.Length);
                    }

                    // Last block is a partial one
                    for (var i = 1; i < 10; ++i)
                    {
                        tarOut.WriteByte((byte)i);
                    }
                }
            }

            var tarData = ms.ToArray();
            Assert.NotNull(tarData);

            // Blocks = Header + Data Blocks + Zero block + Record trailer
            var usedBlocks = 1 + iteration + 2;
            var totalBlocks = usedBlocks + (testBlockFactor - 1);
            totalBlocks /= testBlockFactor;
            totalBlocks *= testBlockFactor;

            Assert.Equal(TarBuffer.BlockSize * totalBlocks, tarData.Length);

            if (usedBlocks < totalBlocks)
            {
                // Start at first byte after header.
                var byteIndex = TarBuffer.BlockSize * (iteration + 1);
                while (byteIndex < tarData.Length)
                {
                    var blockNumber = byteIndex / TarBuffer.BlockSize;
                    var offset = blockNumber % TarBuffer.BlockSize;
                    Assert.True(0 == tarData[byteIndex],
                        $"Trailing block data should be null iteration {iteration} block {blockNumber} offset {offset}  index {byteIndex}");
                    byteIndex += 1;
                }
            }
        }
    }

    private void TryLongName(string name)
    {
        var ms = new MemoryStream();
        using (var tarOut = new TarOutputStream(ms, nameEncoding: null))
        {
            var modTime = DateTime.Now;

            var entry = TarEntry.CreateTarEntry(name);
            tarOut.PutNextEntry(entry);
        }

        var ms2 = new MemoryStream();
        ms2.Write(ms.GetBuffer(), 0, ms.GetBuffer().Length);
        ms2.Seek(0, SeekOrigin.Begin);

        using (var tarIn = new TarInputStream(ms2,  nameEncoding: null))
        {
            var nextEntry = tarIn.GetNextEntry();

            Assert.Equal(nextEntry.Name, name);
        }
    }

    /// <summary>
    /// Check that long names are handled correctly for reading and writing.
    /// </summary>
    [Fact]
    public void LongNames()
    {
        TryLongName("11111111112222222222333333333344444444445555555555" +
                    "6666666666777777777788888888889999999999000000000");

        TryLongName("11111111112222222222333333333344444444445555555555" +
                    "66666666667777777777888888888899999999990000000000");

        TryLongName("11111111112222222222333333333344444444445555555555" +
                    "66666666667777777777888888888899999999990000000000" +
                    "1");

        TryLongName("11111111112222222222333333333344444444445555555555" +
                    "66666666667777777777888888888899999999990000000000" +
                    "11111111112222222222333333333344444444445555555555" +
                    "66666666667777777777888888888899999999990000000000");

        TryLongName("11111111112222222222333333333344444444445555555555" +
                    "66666666667777777777888888888899999999990000000000" +
                    "11111111112222222222333333333344444444445555555555" +
                    "66666666667777777777888888888899999999990000000000" +
                    "11111111112222222222333333333344444444445555555555" +
                    "66666666667777777777888888888899999999990000000000" +
                    "11111111112222222222333333333344444444445555555555" +
                    "66666666667777777777888888888899999999990000000000" +
                    "11111111112222222222333333333344444444445555555555" +
                    "66666666667777777777888888888899999999990000000000");

        for (var n = 1; n < 1024; ++n)
        {
            var format = "{0," + n + "}";
            var formatted = string.Format(format, "A");
            TryLongName(formatted);
        }
    }

    [Fact]
    public void ExtendedHeaderLongName()
    {
        var expectedName = "lftest-0000000000111111111122222222223333333333444444444455555555556666666666777777777788888888889999999999";

        var input64 = @"Li9QYXhIZWFkZXJzLjExOTY5L2xmdGVzdC0wMDAwMDAwMDAwMTExMTExMTExMTIyMjIyMjIyMjIz
							MzMzMzMzMzMzNDQ0NDQ0NDQ0NDU1NTU1NTU1NTU2NjY2NjY2NjY2Nzc3NzAwMDA2NDQAMDAwMDAw
							MAAwMDAwMDAwADAwMDAwMDAwMzE3ADEzMzE2MTYyMzMzADAyMTYwNgAgeAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB1c3RhcgAwMAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAx
							MTcgcGF0aD1sZnRlc3QtMDAwMDAwMDAwMDExMTExMTExMTEyMjIyMjIyMjIyMzMzMzMzMzMzMzQ0
							NDQ0NDQ0NDQ1NTU1NTU1NTU1NjY2NjY2NjY2Njc3Nzc3Nzc3Nzc4ODg4ODg4ODg4OTk5OTk5OTk5
							OQozMCBtdGltZT0xNTMwNDU1MjU5LjcwNjU0ODg4OAozMCBhdGltZT0xNTMwNDU1MjU5LjcwNjU0
							ODg4OAozMCBjdGltZT0xNTMwNDU1MjU5LjcwNjU0ODg4OAoAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGxm
							dGVzdC0wMDAwMDAwMDAwMTExMTExMTExMTIyMjIyMjIyMjIzMzMzMzMzMzMzNDQ0NDQ0NDQ0NDU1
							NTU1NTU1NTU2NjY2NjY2NjY2Nzc3Nzc3Nzc3Nzg4ODg4ODg4ODg5OTkwMDAwNjY0ADAwMDE3NTAA
							MDAwMTc1MAAwMDAwMDAwMDAwMAAxMzMxNjE2MjMzMwAwMjM3MjcAIDAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAdXN0YXIAMDBuaWxzAAAAAAAAAAAAAAAAAAAAAAAA
							AAAAAAAAAAAAAG5pbHMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAMDAwMDAwMAAwMDAwMDAw";

        var buffer = new byte[2560];
        var truncated = Convert.FromBase64String(input64);
        Array.Copy(truncated, buffer, truncated.Length);

        using var ms = new MemoryStream(buffer);
        using var tis = new TarInputStream(ms, nameEncoding: null);
        var entry = tis.GetNextEntry();

        Assert.NotNull(entry);
        Assert.NotNull(entry.Name);
        Assert.Equal(expectedName.Length, entry.Name.Length);
        Assert.Equal(expectedName, entry.Name);
    }

    /// <summary>
    /// Test equals function for tar headers.
    /// </summary>
    [Fact]
    public void HeaderEquality()
    {
        var h1 = new TarHeader();
        var h2 = new TarHeader();

        Assert.True(h1.Equals(h2));

        h1.Name = "ABCDEFG";
        Assert.False(h1.Equals(h2));
        h2.Name = h1.Name;
        Assert.True(h1.Equals(h2));

        h1.Mode = 33188;
        Assert.False(h1.Equals(h2));
        h2.Mode = h1.Mode;
        Assert.True(h1.Equals(h2));

        h1.UserId = 654;
        Assert.False(h1.Equals(h2));
        h2.UserId = h1.UserId;
        Assert.True(h1.Equals(h2));

        h1.GroupId = 654;
        Assert.False(h1.Equals(h2));
        h2.GroupId = h1.GroupId;
        Assert.True(h1.Equals(h2));

        h1.Size = 654;
        Assert.False(h1.Equals(h2));
        h2.Size = h1.Size;
        Assert.True(h1.Equals(h2));

        h1.ModTime = DateTime.Now;
        Assert.False(h1.Equals(h2));
        h2.ModTime = h1.ModTime;
        Assert.True(h1.Equals(h2));

        h1.TypeFlag = 165;
        Assert.False(h1.Equals(h2));
        h2.TypeFlag = h1.TypeFlag;
        Assert.True(h1.Equals(h2));

        h1.LinkName = "link";
        Assert.False(h1.Equals(h2));
        h2.LinkName = h1.LinkName;
        Assert.True(h1.Equals(h2));

        h1.Magic = "other";
        Assert.False(h1.Equals(h2));
        h2.Magic = h1.Magic;
        Assert.True(h1.Equals(h2));

        h1.Version = "1";
        Assert.False(h1.Equals(h2));
        h2.Version = h1.Version;
        Assert.True(h1.Equals(h2));

        h1.UserName = "nuser";
        Assert.False(h1.Equals(h2));
        h2.UserName = h1.UserName;
        Assert.True(h1.Equals(h2));

        h1.GroupName = "group";
        Assert.False(h1.Equals(h2));
        h2.GroupName = h1.GroupName;
        Assert.True(h1.Equals(h2));

        h1.DevMajor = 165;
        Assert.False(h1.Equals(h2));
        h2.DevMajor = h1.DevMajor;
        Assert.True(h1.Equals(h2));

        h1.DevMinor = 164;
        Assert.False(h1.Equals(h2));
        h2.DevMinor = h1.DevMinor;
        Assert.True(h1.Equals(h2));
    }

    [Fact]
    public void Checksum()
    {
        var ms = new MemoryStream();
        using (var tarOut = new TarOutputStream(ms,  nameEncoding: null))
        {
            var entry = TarEntry.CreateTarEntry("TestEntry");
            entry.TarHeader.Mode = 12345;

            tarOut.PutNextEntry(entry);
        }

        var ms2 = new MemoryStream();
        ms2.Write(ms.GetBuffer(), 0, ms.GetBuffer().Length);
        ms2.Seek(0, SeekOrigin.Begin);
        TarEntry nextEntry;

        using (var tarIn = new TarInputStream(ms2, nameEncoding: null))
        {
            nextEntry = tarIn.GetNextEntry();
            Assert.True(nextEntry.TarHeader.IsChecksumValid, "Checksum should be valid");
        }

        var ms3 = new MemoryStream();
        ms3.Write(ms.GetBuffer(), 0, ms.GetBuffer().Length);
        ms3.Seek(0, SeekOrigin.Begin);
        ms3.Write(new byte[] { 34 }, 0, 1);
        ms3.Seek(0, SeekOrigin.Begin);

        using (var tarIn = new TarInputStream(ms3, nameEncoding: null))
        {
            Assert.Throws<TarException>(() => tarIn.GetNextEntry());
        }
    }

    /// <summary>
    /// Check that values set are preserved when writing and reading archives.
    /// </summary>
    [Fact]
    public void ValuesPreserved()
    {
        var ms = new MemoryStream();
        TarEntry entry;
        var modTime = DateTime.Now;

        using (var tarOut = new TarOutputStream(ms, null))
        {
            entry = TarEntry.CreateTarEntry("TestEntry");
            entry.GroupId = 12;
            entry.UserId = 14;
            entry.ModTime = modTime;
            entry.UserName = "UserName";
            entry.GroupName = "GroupName";
            entry.TarHeader.Mode = 12345;

            tarOut.PutNextEntry(entry);
        }

        var ms2 = new MemoryStream();
        ms2.Write(ms.GetBuffer(), 0, ms.GetBuffer().Length);
        ms2.Seek(0, SeekOrigin.Begin);

        using (var tarIn = new TarInputStream(ms2, null))
        {
            var nextEntry = tarIn.GetNextEntry();
            Assert.Equal(entry.TarHeader.Checksum, nextEntry.TarHeader.Checksum);

            Assert.True(nextEntry.Equals(entry), "Entries should be equal");
            Assert.True(nextEntry.TarHeader.Equals(entry.TarHeader), "Headers should match");

            // Tar only stores seconds
            var truncatedTime = new DateTime(modTime.Year, modTime.Month, modTime.Day,
                modTime.Hour, modTime.Minute, modTime.Second);
            Assert.Equal(truncatedTime, nextEntry.ModTime);

            entryCount = 0;
            while (nextEntry != null)
            {
                ++entryCount;
                nextEntry = tarIn.GetNextEntry();
            }

            Assert.Equal(1, entryCount);
        }
    }

    /// <summary>
    /// Check invalid mod times are detected
    /// </summary>
    [Fact]
    public void InvalidModTime()
    {
        var e = TarEntry.CreateTarEntry("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => e.ModTime = DateTime.MinValue);
    }

    /// <summary>
    /// Check invalid sizes are detected
    /// </summary>
    [Fact]
    public void InvalidSize()
    {
        var e = TarEntry.CreateTarEntry("test");
        Assert.Throws<ArgumentOutOfRangeException>(() => e.Size = -6);
    }

    /// <summary>
    /// Check invalid names are detected
    /// </summary>
    [Fact]
    public void InvalidName()
    {
        var e = TarEntry.CreateTarEntry("test");
        Assert.Throws<ArgumentNullException>(() => e.Name = null);
    }

    /// <summary>
    /// Check setting user and group names.
    /// </summary>
    [Fact]
    public void UserAndGroupNames()
    {
        var e = TarEntry.CreateTarEntry("test");
        e.UserName = null;
        Assert.NotNull(e.UserName);
        e.UserName = "";
        Assert.Equal(0, e.UserName.Length);
        e.GroupName = null;
        Assert.Equal("None", e.GroupName);
    }

    /// <summary>
    /// Check invalid magic values are detected
    /// </summary>
    [Fact]
    public void InvalidMagic()
    {
        var e = TarEntry.CreateTarEntry("test");
        Assert.Throws<ArgumentNullException>(() => e.TarHeader.Magic = null);
    }

    /// <summary>
    /// Check invalid link names are detected
    /// </summary>
    [Fact]
    public void InvalidLinkName()
    {
        var e = TarEntry.CreateTarEntry("test");
        Assert.Throws<ArgumentNullException>(() => e.TarHeader.LinkName = null);
    }

    /// <summary>
    /// Check invalid version names are detected
    /// </summary>
    [Fact]
    public void InvalidVersionName()
    {
        var e = TarEntry.CreateTarEntry("test");
        Assert.Throws<ArgumentNullException>(() => e.TarHeader.Version = null);
    }

    [Fact]
    public void CloningAndUniqueness()
    {
        // Partial test of cloning for TarHeader and TarEntry
        var e = TarEntry.CreateTarEntry("ohsogood");
        e.GroupId = 47;
        e.GroupName = "GroupName";
        e.ModTime = DateTime.Now;
        e.Size = 123234;

        var headerE = e.TarHeader;

        headerE.DevMajor = 99;
        headerE.DevMinor = 98;
        headerE.LinkName = "LanceLink";

        var d = (TarEntry)e.Clone();

        Assert.Equal(d.File, e.File);
        Assert.Equal(d.GroupId, e.GroupId);
        Assert.Equal(d.GroupName, e.GroupName);
        Assert.Equal(d.IsDirectory, e.IsDirectory);
        Assert.Equal(d.ModTime, e.ModTime);
        Assert.Equal(d.Size, e.Size);

        var headerD = d.TarHeader;

        Assert.Equal(headerE.Checksum, headerD.Checksum);
        Assert.Equal(headerE.LinkName, headerD.LinkName);

        Assert.Equal(99, headerD.DevMajor);
        Assert.Equal(98, headerD.DevMinor);

        Assert.Equal("LanceLink", headerD.LinkName);

        var entryf = new TarEntry(headerD);

        headerD.LinkName = "Something different";

        Assert.NotEqual(headerD.LinkName, entryf.TarHeader.LinkName);
    }

    [Fact]
    public void OutputStreamOwnership()
    {
        var memStream = new TrackedMemoryStream();
        var s = new TarOutputStream(memStream, null);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.Close();

        Assert.True(memStream.IsClosed, "Should be closed after parent owner close");
        Assert.True(memStream.IsDisposed, "Should be disposed after parent owner close");

        memStream = new TrackedMemoryStream();
        s = new TarOutputStream(memStream, null);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.IsStreamOwner = false;
        s.Close();

        Assert.False(memStream.IsClosed, "Should not be closed after parent owner close");
        Assert.False(memStream.IsDisposed, "Should not be disposed after parent owner close");
    }

    [Fact]
    public void InputStreamOwnership()
    {
        var memStream = new TrackedMemoryStream();
        var s = new TarInputStream(memStream, null);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.Close();

        Assert.True(memStream.IsClosed, "Should be closed after parent owner close");
        Assert.True(memStream.IsDisposed, "Should be disposed after parent owner close");

        memStream = new TrackedMemoryStream();
        s = new TarInputStream(memStream, null);

        Assert.False(memStream.IsClosed, "Shouldnt be closed initially");
        Assert.False(memStream.IsDisposed, "Shouldnt be disposed initially");

        s.IsStreamOwner = false;
        s.Close();

        Assert.False(memStream.IsClosed, "Should not be closed after parent owner close");
        Assert.False(memStream.IsDisposed, "Should not be disposed after parent owner close");
    }

    [Fact]
    public void EndBlockHandling()
    {
        var dummySize = 70145;

        long outCount, inCount;

        using var ms = new MemoryStream();
        using (var tarOut = TarArchive.CreateOutputTarArchive(ms))
        using (var dummyFile = Utils.GetDummyFile(dummySize))
        {
            tarOut.IsStreamOwner = false;
            tarOut.WriteEntry(TarEntry.CreateEntryFromFile(dummyFile), recurse: false);
        }

        outCount = ms.Position;
        ms.Seek(0, SeekOrigin.Begin);

        using (var tarIn = TarArchive.CreateInputTarArchive(ms, nameEncoding: null))
        using (var tempDir = Utils.GetTempDir())
        {
            tarIn.IsStreamOwner = false;
            tarIn.ExtractContents(tempDir);

            foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                Console.WriteLine($"Extracted \"{file}\"");
            }
        }

        inCount = ms.Position;

        Console.WriteLine($"Output count: {outCount}");
        Console.WriteLine($"Input count: {inCount}");

        Assert.Equal(inCount, outCount);
    }

    [Fact(Explicit = true, Skip = "Long Running")]
    [Trait("Category", "Performance")]
    public void WriteThroughput()
    {
        const string entryName = "LargeTarEntry";

        PerformanceTesting.TestWrite(TestDataSize.Large, bs =>
            {
                var tos = new TarOutputStream(bs, nameEncoding: null);
                tos.PutNextEntry(new TarEntry(new TarHeader()
                {
                    Name = entryName,
                    Size = (int)TestDataSize.Large,
                }));
                return tos;
            },
            stream =>
            {
                ((TarOutputStream)stream).CloseEntry();
            });
    }

    [Fact(Explicit = true, Skip = "Long Running")]
    [Trait("Category", "Performance")]
    public void SingleLargeEntry()
    {
        const string entryName = "LargeTarEntry";
        const TestDataSize dataSize = TestDataSize.Large;

        PerformanceTesting.TestReadWrite(
            size: dataSize,
            input: bs =>
            {
                var tis = new TarInputStream(bs, null);
                var entry = tis.GetNextEntry();

                Assert.Equal(entryName, entry.Name);
                return tis;
            },
            output: bs =>
            {
                var tos = new TarOutputStream(bs, null);
                tos.PutNextEntry(new TarEntry(new TarHeader()
                {
                    Name = entryName,
                    Size = (int)dataSize,
                }));
                return tos;
            },
            outputClose: stream =>
            {
                ((TarOutputStream)stream).CloseEntry();
            }
        );
    }

    // Test for corruption issue described @ https://github.com/icsharpcode/SharpZipLib/issues/321
    [Fact]
    public void ExtractingCorruptTarShouldntLeakFiles()
    {
        using var memoryStream = new MemoryStream();
        //Create a tar.gz in the output stream
        using (var gzipStream = new GZipOutputStream(memoryStream))
        {
            gzipStream.IsStreamOwner = false;

            using (var tarOut = TarArchive.CreateOutputTarArchive(gzipStream))
            using (var dummyFile = Utils.GetDummyFile(size: 32000))
            {
                tarOut.IsStreamOwner = false;
                tarOut.WriteEntry(TarEntry.CreateEntryFromFile(dummyFile), recurse: false);
            }
        }

        // corrupt archive - make sure the file still has more than one block
        memoryStream.SetLength(16000);
        memoryStream.Seek(0, SeekOrigin.Begin);

        // try to extract
        using (var gzipStream = new GZipInputStream(memoryStream))
        {
            gzipStream.IsStreamOwner = false;

            using var tempDir = Utils.GetTempDir();
            using (var tarIn = TarArchive.CreateInputTarArchive(gzipStream, nameEncoding: null))
            {
                tarIn.IsStreamOwner = false;
                Assert.Throws<CompressionExceptionBase>(() => tarIn.ExtractContents(tempDir));
            }

            // Try to remove the output directory to check if any file handles are still being held
            var ex = Record.Exception(() => tempDir.Delete());
            Assert.Null(ex);

            Assert.False(tempDir.Exists, "Temporary folder should have been removed");
        }
    }

    [Theory]
    [InlineData(10, "utf-8")]
    [InlineData(10, "shift-jis")]
    public void ParseHeaderWithEncoding(int length, string encodingName)
    {
        // U+3042 is Japanese Hiragana
        // https://unicode.org/charts/PDF/U3040.pdf
        var name = new string((char)0x3042, length);
        var header = new TarHeader();
        var enc = Encoding.GetEncoding(encodingName);
        var headerbytes = new byte[1024];
        var encodedName = enc.GetBytes(name);
        header.Name = name;
        header.WriteHeader(headerbytes, enc);
        var reparseHeader = new TarHeader();
        reparseHeader.ParseBuffer(headerbytes, enc);
        Assert.Equal(name, reparseHeader.Name);
        // top 100 bytes are name field in tar header
        for (var i = 0; i < encodedName.Length; i++)
        {
            Assert.Equal(encodedName[i], headerbytes[i]);
        }
    }

    [Theory]
    [InlineData(1, "utf-8")]
    [InlineData(100, "utf-8")]
    [InlineData(128, "utf-8")]
    [InlineData(1, "shift-jis")]
    [InlineData(100, "shift-jis")]
    [InlineData(128, "shift-jis")]
    public async Task StreamWithJapaneseNameAsync(int length, string encodingName)
    {
        // U+3042 is Japanese Hiragana
        // https://unicode.org/charts/PDF/U3040.pdf
        var entryName = new string((char)0x3042, length);
        var data = new byte[32];
        var encoding = Encoding.GetEncoding(encodingName);
        using var memoryStream = new MemoryStream();
        await using (var tarOutput = new TarOutputStream(memoryStream, encoding))
        {
            var entry = TarEntry.CreateTarEntry(entryName);
            entry.Size = 32;
            tarOutput.PutNextEntry(entry);
            tarOutput.Write(data, 0, data.Length);
        }

        using(var memInput = new MemoryStream(memoryStream.ToArray()))
        await using(var inputStream = new TarInputStream(memInput, encoding))
        {
            var buf = new byte[64];
            var entry = await inputStream.GetNextEntryAsync(CancellationToken.None);
            Assert.Equal(entryName, entry.Name);
            var bytesread = await inputStream.ReadAsync(buf, 0, buf.Length, CancellationToken.None);
            Assert.Equal(data.Length, bytesread);
        }
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), $"jpnametest_{length}_{encodingName}.tar"), memoryStream.ToArray());
    }
    /// <summary>
    /// This test could be considered integration test. it creates a tar archive with the root directory specified
    /// Then extracts it and compares the two folders. This used to fail on unix due to issues with root folder handling
    /// in the tar archive.
    /// </summary>
    [Fact]
    public void RootPathIsRespected()
    {
        using var extractDirectory = new TempDir();
        using var tarFileName = new TempFile();
        using var tempDirectory = new TempDir();
        tempDirectory.CreateDummyFile();

        using (var tarFile = File.Open(tarFileName.FullName, FileMode.Create))
        {
            using (var tarOutputStream = TarArchive.CreateOutputTarArchive(tarFile))
            {
                tarOutputStream.RootPath = tempDirectory.FullName;
                var entry = TarEntry.CreateEntryFromFile(tempDirectory.FullName);
                tarOutputStream.WriteEntry(entry, true);
            }
        }

        using (var file = File.OpenRead(tarFileName.FullName))
        {
            using (var archive = TarArchive.CreateInputTarArchive(file, Encoding.UTF8))
            {
                archive.ExtractContents(extractDirectory.FullName);
            }
        }

        var expectationDirectory = new DirectoryInfo(tempDirectory.FullName);
        foreach (var checkFile in expectationDirectory.GetFiles("", SearchOption.AllDirectories))
        {
            var relativePath = checkFile.FullName.Substring(expectationDirectory.FullName.Length + 1);
            Assert.True(File.Exists(Path.Combine(extractDirectory.FullName, relativePath)));
            Assert.Equal(File.ReadAllBytes(checkFile.FullName), File.ReadAllBytes(Path.Combine(extractDirectory.FullName, relativePath)));
        }
    }
}
