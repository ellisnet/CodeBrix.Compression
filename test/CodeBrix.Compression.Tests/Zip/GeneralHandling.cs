using CodeBrix.Compression.Tests.TestSupport;
using CodeBrix.Compression.Zip;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using Does = CodeBrix.Compression.Tests.TestSupport.Does;

namespace CodeBrix.Compression.Tests.Zip;

/// <summary>
/// This class contains test cases for Zip compression and decompression
/// </summary>
[TestFixture]
public class GeneralHandling : ZipBase
{
    private void ExerciseZip(CompressionMethod method, int compressionLevel,
        int size, string password, bool canSeek)
    {
        byte[] originalData = null;
        var compressedData = MakeInMemoryZip(ref originalData, method, compressionLevel, size, password, canSeek);

        var ms = new MemoryStream(compressedData);
        ms.Seek(0, SeekOrigin.Begin);

        using var inStream = new ZipInputStream(ms);
        var decompressedData = new byte[size];
        if (password != null)
        {
            inStream.Password = password;
        }

        var entry2 = inStream.GetNextEntry();

        if ((entry2.Flags & 8) == 0)
        {
            ClassicAssert.AreEqual(size, entry2.Size, "Entry size invalid");
        }

        var currentIndex = 0;

        if (size > 0)
        {
            var count = decompressedData.Length;

            while (true)
            {
                var numRead = inStream.Read(decompressedData, currentIndex, count);
                if (numRead <= 0)
                {
                    break;
                }
                currentIndex += numRead;
                count -= numRead;
            }
        }

        ClassicAssert.AreEqual(currentIndex, size, "Original and decompressed data different sizes");

        if (originalData != null)
        {
            for (var i = 0; i < originalData.Length; ++i)
            {
                ClassicAssert.AreEqual(decompressedData[i], originalData[i], "Decompressed data doesnt match original, compression level: " + compressionLevel);
            }
        }
    }

    /// <summary>
    /// Invalid passwords should be detected early if possible, seekable stream
    /// Note: Have a 1/255 chance of failing due to CRC collision (hence retried once)
    /// </summary>
    [Test]
    [Category("Zip")]
    [Retry(2)]
    public void InvalidPasswordSeekable()
    {
        byte[] originalData = null;
        var compressedData = MakeInMemoryZip(ref originalData, CompressionMethod.Deflated, 3, 500, "Hola", true);

        var ms = new MemoryStream(compressedData);
        ms.Seek(0, SeekOrigin.Begin);

        var buf2 = new byte[originalData.Length];
        var pos = 0;

        var inStream = new ZipInputStream(ms);
        inStream.Password = "redhead";

        var entry2 = inStream.GetNextEntry();

        Assert.Throws<ZipException>(() =>
        {
            while (true)
            {
                var numRead = inStream.Read(buf2, pos, buf2.Length);
                if (numRead <= 0)
                {
                    break;
                }
                pos += numRead;
            }
        });
    }

    /// <summary>
    /// Check that GetNextEntry can handle the situation where part of the entry data has been read
    /// before the call is made.  ZipInputStream.CloseEntry wasnt handling this at all.
    /// </summary>
    [Test]
    [Category("Zip")]
    public void ExerciseGetNextEntry()
    {
        var compressedData = MakeInMemoryZip(
            true,
            new RuntimeInfo(CompressionMethod.Deflated, 9, 50, null, true),
            new RuntimeInfo(CompressionMethod.Deflated, 2, 50, null, true),
            new RuntimeInfo(CompressionMethod.Deflated, 9, 50, null, true),
            new RuntimeInfo(CompressionMethod.Deflated, 2, 50, null, true),
            new RuntimeInfo(null, true),
            new RuntimeInfo(CompressionMethod.Stored, 2, 50, null, true),
            new RuntimeInfo(CompressionMethod.Deflated, 9, 50, null, true)
        );

        var ms = new MemoryStream(compressedData);
        ms.Seek(0, SeekOrigin.Begin);

        using var inStream = new ZipInputStream(ms);
        var buffer = new byte[10];

        while (inStream.GetNextEntry() != null)
        {
            // Read a portion of the data, so GetNextEntry has some work to do.
            inStream.ReadAtLeast(buffer, minimumBytes: 1, throwOnEndOfStream: false);
        }
    }

    /// <summary>
    /// Invalid passwords should be detected early if possible, non seekable stream
    /// Note: Have a 1/255 chance of failing due to CRC collision (hence retried once)
    /// </summary>
    [Test]
    [Category("Zip")]
    [Retry(2)]
    public void InvalidPasswordNonSeekable()
    {
        byte[] originalData = null;
        var compressedData = MakeInMemoryZip(ref originalData, CompressionMethod.Deflated, 3, 500, "Hola", false);

        var ms = new MemoryStream(compressedData);
        ms.Seek(0, SeekOrigin.Begin);

        var buf2 = new byte[originalData.Length];
        var pos = 0;

        var inStream = new ZipInputStream(ms);
        inStream.Password = "redhead";

        var entry2 = inStream.GetNextEntry();

        Assert.Throws<ZipException>(() =>
        {
            while (true)
            {
                var numRead = inStream.Read(buf2, pos, buf2.Length);
                if (numRead <= 0)
                {
                    break;
                }
                pos += numRead;
            }
        });
    }

    /// <summary>
    /// Adding an entry after the stream has Finished should fail
    /// </summary>
    [Test]
    [Category("Zip")]
    //[ExpectedException(typeof(InvalidOperationException))]
    public void AddEntryAfterFinish()
    {
        var ms = new MemoryStream();
        var s = new ZipOutputStream(ms);
        s.Finish();
        //s.PutNextEntry(new ZipEntry("dummyfile.tst"));

        Assert.That(() => s.PutNextEntry(new ZipEntry("dummyfile.tst")),
            Throws.TypeOf<InvalidOperationException>());
    }

    /// <summary>
    /// Test setting file commment to a value that is too long
    /// </summary>
    [Test]
    [Category("Zip")]
    //[ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void SetCommentOversize()
    {
        var ms = new MemoryStream();
        var s = new ZipOutputStream(ms);
        //s.SetComment(new String('A', 65536));

        Assert.That(() => s.SetComment(new String('A', 65536)),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    /// <summary>
    /// Check that simply closing ZipOutputStream finishes the zip correctly
    /// </summary>
    [Test]
    [Category("Zip")]
    public void CloseOnlyHandled()
    {
        var ms = new MemoryStream();
        var s = new ZipOutputStream(ms);
        s.PutNextEntry(new ZipEntry("dummyfile.tst"));
        s.Close();

        ClassicAssert.IsTrue(s.IsFinished, "Output stream should be finished");
    }

    /// <summary>
    /// Basic compress/decompress test, no encryption, size is important here as its big enough
    /// to force multiple write to output which was a problem...
    /// </summary>
    [Test]
    [Category("Zip")]
    public void BasicDeflated()
    {
        for (var i = 0; i <= 9; ++i)
        {
            ExerciseZip(CompressionMethod.Deflated, i, 50000, null, true);
        }
    }

    /// <summary>
    /// Basic compress/decompress test, no encryption, size is important here as its big enough
    /// to force multiple write to output which was a problem...
    /// </summary>
    [Test]
    [Category("Zip")]
    public void BasicDeflatedNonSeekable()
    {
        for (var i = 0; i <= 9; ++i)
        {
            ExerciseZip(CompressionMethod.Deflated, i, 50000, null, false);
        }
    }

    /// <summary>
    /// Basic stored file test, no encryption.
    /// </summary>
    [Test]
    [Category("Zip")]
    public void BasicStored()
    {
        ExerciseZip(CompressionMethod.Stored, 0, 50000, null, true);
    }

    /// <summary>
    /// Basic stored file test, no encryption, non seekable output
    /// NOTE this gets converted to deflate level 0
    /// </summary>
    [Test]
    [Category("Zip")]
    public void BasicStoredNonSeekable()
    {
        ExerciseZip(CompressionMethod.Stored, 0, 50000, null, false);
    }

    [Test]
    [Category("Zip")]
    [TestCase(21348, null)]
    [TestCase(24692, "Mabutu")]
    public void StoredNonSeekableKnownSizeNoCrc(int targetSize, string password)
    {
        // This cannot be stored directly as the crc is not known.

        MemoryStream ms = new MemoryStreamWithoutSeek();

        using (var outStream = new ZipOutputStream(ms))
        {
            outStream.Password = password;
            if (!string.IsNullOrEmpty(password))
            {
                outStream.IsUnderTest = true;
            }
            outStream.IsStreamOwner = false;
            var entry = new ZipEntry("dummyfile.tst");
            entry.CompressionMethod = CompressionMethod.Stored;

            // The bit thats in question is setting the size before its added to the archive.
            entry.Size = targetSize;

            outStream.PutNextEntry(entry);

            ClassicAssert.AreEqual(CompressionMethod.Deflated, entry.CompressionMethod, "Entry should be deflated");
            ClassicAssert.AreEqual(-1, entry.CompressedSize, "Compressed size should be known");

            var original = Utils.GetDummyBytes(targetSize);

            // Although this could be written in one chunk doing it in lumps
            // throws up buffering problems including with encryption the original
            // source for this change.
            int index = 0, size = targetSize;
            while (size > 0)
            {
                var count = (size > 0x200) ? 0x200 : size;
                outStream.Write(original, index, count);
                size -= 0x200;
                index += count;
            }
        }
        Assert.That(ms.ToArray(), Does.PassTestArchive(password));
    }

    /// <summary>
    /// Basic compress/decompress test, with encryption, size is important here as its big enough
    /// to force multiple writes to output which was a problem...
    /// </summary>
    [Test]
    [Category("Zip")]
    public void BasicDeflatedEncrypted()
    {
        for (var i = 0; i <= 9; ++i)
        {
            ExerciseZip(CompressionMethod.Deflated, i, 50157, "Rosebud", true);
        }
    }

    /// <summary>
    /// Basic compress/decompress test, with encryption, size is important here as its big enough
    /// to force multiple write to output which was a problem...
    /// </summary>
    [Test]
    [Category("Zip")]
    public void BasicDeflatedEncryptedNonSeekable()
    {
        for (var i = 0; i <= 9; ++i)
        {
            ExerciseZip(CompressionMethod.Deflated, i, 50000, "Rosebud", false);
        }
    }

    [Test]
    [Category("Zip")]
    public void SkipEncryptedEntriesWithoutSettingPassword()
    {
        var compressedData = MakeInMemoryZip(true,
            new RuntimeInfo("1234", true),
            new RuntimeInfo(CompressionMethod.Deflated, 2, 1, null, true),
            new RuntimeInfo(CompressionMethod.Deflated, 9, 1, "1234", true),
            new RuntimeInfo(CompressionMethod.Deflated, 2, 1, null, true),
            new RuntimeInfo(null, true),
            new RuntimeInfo(CompressionMethod.Stored, 2, 1, "4321", true),
            new RuntimeInfo(CompressionMethod.Deflated, 9, 1, "1234", true)
        );

        var ms = new MemoryStream(compressedData);
        var inStream = new ZipInputStream(ms);

        while (inStream.GetNextEntry() != null)
        {
        }

        inStream.Close();
    }

    [Test]
    [Category("Zip")]
    public void MixedEncryptedAndPlain()
    {
        var compressedData = MakeInMemoryZip(true,
            new RuntimeInfo(CompressionMethod.Deflated, 2, 1, null, true),
            new RuntimeInfo(CompressionMethod.Deflated, 9, 1, "1234", false),
            new RuntimeInfo(CompressionMethod.Deflated, 2, 1, null, false),
            new RuntimeInfo(CompressionMethod.Deflated, 9, 1, "1234", true)
        );

        var ms = new MemoryStream(compressedData);
        using var inStream = new ZipInputStream(ms);
        inStream.Password = "1234";

        var extractCount = 0;
        var extractIndex = 0;

        var decompressedData = new byte[100];

        while (inStream.GetNextEntry() != null)
        {
            extractCount = decompressedData.Length;
            extractIndex = 0;
            while (true)
            {
                var numRead = inStream.Read(decompressedData, extractIndex, extractCount);
                if (numRead <= 0)
                {
                    break;
                }
                extractIndex += numRead;
                extractCount -= numRead;
            }
        }
        inStream.Close();
    }

    /// <summary>
    /// Basic stored file test, with encryption.
    /// </summary>
    [Test]
    [Category("Zip")]
    public void BasicStoredEncrypted()
    {
        ExerciseZip(CompressionMethod.Stored, compressionLevel: 0, size: 50000, "Rosebud", canSeek: true);
    }

    /// <summary>
    /// Basic stored file test, with encryption, non seekable output.
    /// NOTE this gets converted deflate level 0
    /// </summary>
    [Test]
    [Category("Zip")]
    public void BasicStoredEncryptedNonSeekable()
    {
        ExerciseZip(CompressionMethod.Stored, compressionLevel: 0, size: 50000, "Rosebud", canSeek: false);
    }

    /// <summary>
    /// Check that when the output stream cannot seek that requests for stored
    /// are in fact converted to defalted level 0
    /// </summary>
    [Test]
    [Category("Zip")]
    public void StoredNonSeekableConvertToDeflate()
    {
        var ms = new MemoryStreamWithoutSeek();

        var outStream = new ZipOutputStream(ms);
        outStream.SetLevel(8);
        ClassicAssert.AreEqual(8, outStream.GetLevel(), "Compression level invalid");

        var entry = new ZipEntry("1.tst");
        entry.CompressionMethod = CompressionMethod.Stored;
        outStream.PutNextEntry(entry);
        ClassicAssert.AreEqual(0, outStream.GetLevel(), "Compression level invalid");

        Utils.WriteDummyData(outStream, 100);
        entry = new ZipEntry("2.tst");
        entry.CompressionMethod = CompressionMethod.Deflated;
        outStream.PutNextEntry(entry);
        ClassicAssert.AreEqual(8, outStream.GetLevel(), "Compression level invalid");
        Utils.WriteDummyData(outStream, 100);

        outStream.Close();
    }

    /// <summary>
    /// Check that Unicode filename support works.
    /// </summary>
    [Test]
    [Category("Zip")]
    public void Stream_UnicodeEntries()
    {
        var ms = new MemoryStream();
        using var s = new ZipOutputStream(ms);
        s.IsStreamOwner = false;

        var sampleName = "\u03A5\u03d5\u03a3";
        var sample = new ZipEntry(sampleName);
        sample.IsUnicodeText = true;
        s.PutNextEntry(sample);

        s.Finish();
        ms.Seek(0, SeekOrigin.Begin);

        using var zis = new ZipInputStream(ms);
        var ze = zis.GetNextEntry();
        ClassicAssert.AreEqual(sampleName, ze.Name, "Expected name to match original");
        ClassicAssert.IsTrue(ze.IsUnicodeText, "Expected IsUnicodeText flag to be set");
    }

    [Test]
    [Category("Zip")]
    [Category("CreatesTempFile")]
    public void PartialStreamClosing()
    {
        var tempFile = GetTempFilePath();
        ClassicAssert.IsNotNull(tempFile, "No permission to execute this test?");

        if (tempFile != null)
        {
            tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");
            MakeZipFile(tempFile, new String[] { "Farriera", "Champagne", "Urban myth" }, 10, "Aha");

            using (var zipFile = new ZipFile(tempFile))
            {
                var stream = zipFile.GetInputStream(0);
                stream.Close();

                stream = zipFile.GetInputStream(1);
                zipFile.Close();
            }
            File.Delete(tempFile);
        }
    }

    private void TestLargeZip(string tempFile, int targetFiles)
    {
        const int BlockSize = 4096;

        var data = new byte[BlockSize];
        byte nextValue = 0;
        for (var i = 0; i < BlockSize; ++i)
        {
            nextValue = ScatterValue(nextValue);
            data[i] = nextValue;
        }

        using var zFile = new ZipFile(tempFile);
        ClassicAssert.AreEqual(targetFiles, zFile.Count);
        var readData = new byte[BlockSize];
        int readIndex;
        foreach (var ze in zFile)
        {
            var s = zFile.GetInputStream(ze);
            readIndex = 0;
            while (readIndex < readData.Length)
            {
                readIndex += s.Read(readData, readIndex, data.Length - readIndex);
            }

            for (var ii = 0; ii < BlockSize; ++ii)
            {
                ClassicAssert.AreEqual(data[ii], readData[ii]);
            }
        }
        zFile.Close();
    }

    //      [Test]
    //      [Category("Zip")]
    //      [Category("CreatesTempFile")]
    public void TestLargeZipFile()
    {
        var tempFile = @"g:\\tmp";
        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");
        TestLargeZip(tempFile, 8100);
    }

    //      [Test]
    //      [Category("Zip")]
    //      [Category("CreatesTempFile")]
    public void MakeLargeZipFile()
    {
        string tempFile = null;
        try
        {
            //            tempFile = Path.GetTempPath();
            tempFile = @"g:\\tmp";
        }
        catch (SecurityException)
        {
        }

        ClassicAssert.IsNotNull(tempFile, "No permission to execute this test?");

        const int blockSize = 4096;

        var data = new byte[blockSize];
        byte nextValue = 0;
        for (var i = 0; i < blockSize; ++i)
        {
            nextValue = ScatterValue(nextValue);
            data[i] = nextValue;
        }

        tempFile = Path.Combine(tempFile, "SharpZipTest.Zip");
        Console.WriteLine("Starting at {0}", DateTime.Now);
        try
        {
            //               MakeZipFile(tempFile, new String[] {"1", "2" }, int.MaxValue, "C1");
            using var fs = File.Create(tempFile);
            var zOut = new ZipOutputStream(fs);
            zOut.SetLevel(4);
            const int TargetFiles = 8100;
            for (var i = 0; i < TargetFiles; ++i)
            {
                var e = new ZipEntry(i.ToString());
                e.CompressionMethod = CompressionMethod.Stored;

                zOut.PutNextEntry(e);
                for (var block = 0; block < 128; ++block)
                {
                    zOut.Write(data, 0, blockSize);
                }
            }
            zOut.Close();
            fs.Close();

            TestLargeZip(tempFile, TargetFiles);
        }
        finally
        {
            Console.WriteLine("Starting at {0}", DateTime.Now);
            //               File.Delete(tempFile);
        }
    }

    /*

    /// <summary>
    /// Test for handling of zero lengths in compression using a formatter which
    /// will request reads of zero length...
    /// </summary>
    [Test]
    [Category("Zip")]
    [Ignore("With ArraySegment<byte> for crc checking, this test doesn't throw an exception. Not sure if it's needed.")]
    public void SerializedObjectZeroLength()
    {
        var exception = false;

        object data = new byte[0];
        // Thisa wont be zero length here due to serialisation.
        try
        {
            var zipped = ZipZeroLength(data);

            var o = UnZipZeroLength(zipped);

            var returned = o as byte[];

            ClassicAssert.IsNotNull(returned, "Expected a byte[]");
            ClassicAssert.AreEqual(0, returned.Length);
        }
        catch (ArgumentOutOfRangeException)
        {
            exception = true;
        }

        ClassicAssert.IsTrue(exception, "Passing an offset greater than or equal to buffer.Length should cause an ArgumentOutOfRangeException");
    }

    */

    /// <summary>
    /// Test for handling of serialized reference and value objects.
    /// </summary>
    [Test]
    [Category("Zip")]
    public void SerializedObject()
    {
        var sampleDateTime = new DateTime(1853, 8, 26);
        var zipped = ZipZeroLength(sampleDateTime);
        var returnedDateTime = UnZipZeroLength<DateTime>(zipped);

        ClassicAssert.AreEqual(sampleDateTime, returnedDateTime);

        var sampleString = "Mary had a giant cat it ears were green and smelly";
        zipped = ZipZeroLength(sampleString);

        var returnedString = UnZipZeroLength<string>(zipped);

        ClassicAssert.AreEqual(sampleString, returnedString);
    }

    private byte[] ZipZeroLength(object data)
    {
        var memStream = new MemoryStream();

        using (ZipOutputStream zipStream = new ZipOutputStream(memStream))
        {
            zipStream.PutNextEntry(new ZipEntry("data"));
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(data, data.GetType());
            zipStream.Write(jsonBytes, 0, jsonBytes.Length);
            zipStream.CloseEntry();
            zipStream.Close();
        }

        byte[] result = memStream.ToArray();
        memStream.Close();

        return result;
    }

    private T UnZipZeroLength<T>(byte[] zipped)
    {
        if (zipped == null)
        {
            return default;
        }

        T result = default;
        var memStream = new MemoryStream(zipped);
        using (ZipInputStream zipStream = new ZipInputStream(memStream))
        {
            ZipEntry zipEntry = zipStream.GetNextEntry();
            if (zipEntry != null)
            {
                using var jsonStream = new MemoryStream();
                zipStream.CopyTo(jsonStream);
                jsonStream.Position = 0;
                result = JsonSerializer.Deserialize<T>(jsonStream);
            }
            zipStream.Close();
        }
        memStream.Close();

        return result;
    }

    [Test]
    [Category("Zip")]
    [TestCase("Hello")]
    [TestCase("a/b/c/d/e/f/g/h/SomethingLikeAnArchiveName.txt")]
    public void LegacyNameConversion(string name)
    {
        var encoding = StringCodec.Default.ZipEncoding(false);
        var intermediate = encoding.GetBytes(name);
        var final = encoding.GetString(intermediate);

        ClassicAssert.AreEqual(name, final, "Expected identical result");
    }

    [Test]
    [Category("Zip")]
    public void UnicodeNameConversion()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var codec = StringCodec.FromCodePage(850);
        var sample = "Hello world";

        var rawData = Encoding.ASCII.GetBytes(sample);

        var converted = codec.LegacyEncoding.GetString(rawData);
        ClassicAssert.AreEqual(sample, converted);

        converted = codec.ZipInputEncoding(GeneralBitFlags.UnicodeText).GetString(rawData);
        ClassicAssert.AreEqual(sample, converted);

        // This time use some greek characters
        sample = "\u03A5\u03d5\u03a3";
        rawData = Encoding.UTF8.GetBytes(sample);

        converted = codec.ZipInputEncoding(GeneralBitFlags.UnicodeText).GetString(rawData);
        ClassicAssert.AreEqual(sample, converted);
    }

    /*

    /// <summary>
    /// Regression test for problem where the password check would fail for an archive whose
    /// date was updated from the extra data.
    /// This applies to archives where the crc wasnt know at the time of encryption.
    /// The date of the entry is used in its place.
    /// </summary>
    [Test]
    [Category("Zip")]
    [Ignore("at commit 60831547c868cc56d43f24473f7d5f2cc51fb754 this unit test passed but the behavior of ZipEntry.DateTime has changed completely ever since. Not sure if this unit test is still needed.")]
    public void PasswordCheckingWithDateInExtraData()
    {
        var ms = new MemoryStream();
        var checkTime = new DateTimeOffset(2010, 10, 16, 0, 3, 28, new TimeSpan(1, 0, 0));

        using (ZipOutputStream zos = new ZipOutputStream(ms))
        {
            zos.IsStreamOwner = false;
            zos.Password = "secret";
            var ze = new ZipEntry("uno");
            ze.DateTime = new DateTime(1998, 6, 5, 4, 3, 2);

            var zed = new ZipExtraData();

            zed.StartNewEntry();

            zed.AddData(1);

            TimeSpan delta = checkTime.UtcDateTime - new DateTime(1970, 1, 1, 0, 0, 0).ToUniversalTime();
            var seconds = (int)delta.TotalSeconds;
            zed.AddLeInt(seconds);
            zed.AddNewEntry(0x5455);

            ze.ExtraData = zed.GetEntryData();
            zos.PutNextEntry(ze);
            zos.WriteByte(54);
        }

        ms.Position = 0;
        using (ZipInputStream zis = new ZipInputStream(ms))
        {
            zis.Password = "secret";
            ZipEntry uno = zis.GetNextEntry();
            var theByte = (byte)zis.ReadByte();
            ClassicAssert.AreEqual(54, theByte);
            ClassicAssert.AreEqual(-1, zis.ReadByte()); // eof
            ClassicAssert.AreEqual(checkTime.DateTime, uno.DateTime);
        }
    }

    */
}