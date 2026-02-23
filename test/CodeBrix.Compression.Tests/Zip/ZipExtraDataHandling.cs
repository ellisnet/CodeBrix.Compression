using CodeBrix.Compression.Zip;
using NUnit.Framework;
using System;
using System.IO;
using NUnit.Framework.Legacy;

namespace CodeBrix.Compression.Tests.Zip;

[TestFixture]
public class ZipExtraDataHandling : ZipBase
{
    /// <summary>
    /// Extra data for separate entries should be unique to that entry
    /// </summary>
    [Test]
    [Category("Zip")]
    public void IsDataUnique()
    {
        var a = new ZipEntry("Basil");
        var extra = new byte[4];
        extra[0] = 27;
        a.ExtraData = extra;

        var b = (ZipEntry)a.Clone();
        b.ExtraData[0] = 89;
        ClassicAssert.IsTrue(b.ExtraData[0] != a.ExtraData[0], "Extra data not unique " + b.ExtraData[0] + " " + a.ExtraData[0]);

        var c = (ZipEntry)a.Clone();
        c.ExtraData[0] = 45;
        ClassicAssert.IsTrue(a.ExtraData[0] != c.ExtraData[0], "Extra data not unique " + a.ExtraData[0] + " " + c.ExtraData[0]);
    }

    [Test]
    [Category("Zip")]
    public void ExceedSize()
    {
        var zed = new ZipExtraData();
        var buffer = new byte[65506];
        zed.AddEntry(1, buffer);
        ClassicAssert.AreEqual(65510, zed.Length);
        zed.AddEntry(2, new byte[21]);
        ClassicAssert.AreEqual(65535, zed.Length);

        var caught = false;
        try
        {
            zed.AddEntry(3, null);
        }
        catch
        {
            caught = true;
        }

        ClassicAssert.IsTrue(caught, "Expected an exception when max size exceeded");
        ClassicAssert.AreEqual(65535, zed.Length);

        zed.Delete(2);
        ClassicAssert.AreEqual(65510, zed.Length);

        caught = false;
        try
        {
            zed.AddEntry(2, new byte[22]);
        }
        catch
        {
            caught = true;
        }
        ClassicAssert.IsTrue(caught, "Expected an exception when max size exceeded");
        ClassicAssert.AreEqual(65510, zed.Length);
    }

    [Test]
    [Category("Zip")]
    public void Deleting()
    {
        var zed = new ZipExtraData();
        ClassicAssert.AreEqual(0, zed.Length);

        // Tag 1 Totoal length 10
        zed.AddEntry(1, new byte[] { 10, 11, 12, 13, 14, 15 });
        ClassicAssert.AreEqual(10, zed.Length, "Length should be 10");
        ClassicAssert.AreEqual(10, zed.GetEntryData().Length, "Data length should be 10");

        // Tag 2 total length  9
        zed.AddEntry(2, new byte[] { 20, 21, 22, 23, 24 });
        ClassicAssert.AreEqual(19, zed.Length, "Length should be 19");
        ClassicAssert.AreEqual(19, zed.GetEntryData().Length, "Data length should be 19");

        // Tag 3 Total Length 6
        zed.AddEntry(3, new byte[] { 30, 31 });
        ClassicAssert.AreEqual(25, zed.Length, "Length should be 25");
        ClassicAssert.AreEqual(25, zed.GetEntryData().Length, "Data length should be 25");

        zed.Delete(2);
        ClassicAssert.AreEqual(16, zed.Length, "Length should be 16");
        ClassicAssert.AreEqual(16, zed.GetEntryData().Length, "Data length should be 16");

        // Tag 2 total length  9
        zed.AddEntry(2, new byte[] { 20, 21, 22, 23, 24 });
        ClassicAssert.AreEqual(25, zed.Length, "Length should be 25");
        ClassicAssert.AreEqual(25, zed.GetEntryData().Length, "Data length should be 25");

        zed.AddEntry(3, null);
        ClassicAssert.AreEqual(23, zed.Length, "Length should be 23");
        ClassicAssert.AreEqual(23, zed.GetEntryData().Length, "Data length should be 23");
    }

    [Test]
    [Category("Zip")]
    public void BasicOperations()
    {
        var zed = new ZipExtraData(null);
        ClassicAssert.AreEqual(0, zed.Length);

        zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        ClassicAssert.AreEqual(4, zed.Length, "A length should be 4");

        var zed2 = new ZipExtraData();
        ClassicAssert.AreEqual(0, zed2.Length);

        zed2.AddEntry(1, new byte[] { });

        var data = zed.GetEntryData();
        for (var i = 0; i < data.Length; ++i)
        {
            ClassicAssert.AreEqual(zed2.GetEntryData()[i], data[i]);
        }

        ClassicAssert.AreEqual(4, zed2.Length, "A1 length should be 4");

        var findResult = zed.Find(2);
        ClassicAssert.IsFalse(findResult, "A - Shouldnt find tag 2");

        findResult = zed.Find(1);
        ClassicAssert.IsTrue(findResult, "A - Should find tag 1");
        ClassicAssert.AreEqual(0, zed.ValueLength, "A- Length of entry should be 0");
        ClassicAssert.AreEqual(-1, zed.ReadByte());
        ClassicAssert.AreEqual(0, zed.GetStreamForTag(1).Length, "A - Length of stream should be 0");

        zed = new ZipExtraData(new byte[] { 1, 0, 3, 0, 1, 2, 3 });
        ClassicAssert.AreEqual(7, zed.Length, "Expected a length of 7");

        findResult = zed.Find(1);
        ClassicAssert.IsTrue(findResult, "B - Should find tag 1");
        ClassicAssert.AreEqual(3, zed.ValueLength, "B - Length of entry should be 3");
        for (var i = 1; i <= 3; ++i)
        {
            ClassicAssert.AreEqual(i, zed.ReadByte());
        }
        ClassicAssert.AreEqual(-1, zed.ReadByte());

        var s = zed.GetStreamForTag(1);
        ClassicAssert.AreEqual(3, s.Length, "B.1 Stream length should be 3");
        for (var i = 1; i <= 3; ++i)
        {
            ClassicAssert.AreEqual(i, s.ReadByte());
        }
        ClassicAssert.AreEqual(-1, s.ReadByte());

        zed = new ZipExtraData(new byte[] { 1, 0, 3, 0, 1, 2, 3, 2, 0, 1, 0, 56 });
        ClassicAssert.AreEqual(12, zed.Length, "Expected a length of 12");

        findResult = zed.Find(1);
        ClassicAssert.IsTrue(findResult, "C.1 - Should find tag 1");
        ClassicAssert.AreEqual(3, zed.ValueLength, "C.1 - Length of entry should be 3");
        for (var i = 1; i <= 3; ++i)
        {
            ClassicAssert.AreEqual(i, zed.ReadByte());
        }
        ClassicAssert.AreEqual(-1, zed.ReadByte());

        findResult = zed.Find(2);
        ClassicAssert.IsTrue(findResult, "C.2 - Should find tag 2");
        ClassicAssert.AreEqual(1, zed.ValueLength, "C.2 - Length of entry should be 1");
        ClassicAssert.AreEqual(56, zed.ReadByte());
        ClassicAssert.AreEqual(-1, zed.ReadByte());

        s = zed.GetStreamForTag(2);
        ClassicAssert.AreEqual(1, s.Length);
        ClassicAssert.AreEqual(56, s.ReadByte());
        ClassicAssert.AreEqual(-1, s.ReadByte());

        zed = new ZipExtraData();
        zed.AddEntry(7, new byte[] { 33, 44, 55 });
        findResult = zed.Find(7);
        ClassicAssert.IsTrue(findResult, "Add.1 should find new tag");
        ClassicAssert.AreEqual(3, zed.ValueLength, "Add.1 length should be 3");
        ClassicAssert.AreEqual(33, zed.ReadByte());
        ClassicAssert.AreEqual(44, zed.ReadByte());
        ClassicAssert.AreEqual(55, zed.ReadByte());
        ClassicAssert.AreEqual(-1, zed.ReadByte());

        zed.AddEntry(7, null);
        findResult = zed.Find(7);
        ClassicAssert.IsTrue(findResult, "Add.2 should find new tag");
        ClassicAssert.AreEqual(0, zed.ValueLength, "Add.2 length should be 0");

        zed.StartNewEntry();
        zed.AddData(0xae);
        zed.AddNewEntry(55);

        findResult = zed.Find(55);
        ClassicAssert.IsTrue(findResult, "Add.3 should find new tag");
        ClassicAssert.AreEqual(1, zed.ValueLength, "Add.3 length should be 1");
        ClassicAssert.AreEqual(0xae, zed.ReadByte());
        ClassicAssert.AreEqual(-1, zed.ReadByte());

        zed = new ZipExtraData();
        zed.StartNewEntry();
        zed.AddLeLong(0);
        zed.AddLeLong(-4);
        zed.AddLeLong(-1);
        zed.AddLeLong(long.MaxValue);
        zed.AddLeLong(long.MinValue);
        zed.AddLeLong(0x123456789ABCDEF0);
        zed.AddLeLong(unchecked((long)0xFEDCBA9876543210));
        zed.AddNewEntry(567);

        s = zed.GetStreamForTag(567);
        var longValue = ReadLong(s);
        ClassicAssert.AreEqual(longValue, zed.ReadLong(), "Read/stream mismatch");
        ClassicAssert.AreEqual(0, longValue, "Expected long value of zero");

        longValue = ReadLong(s);
        ClassicAssert.AreEqual(longValue, zed.ReadLong(), "Read/stream mismatch");
        ClassicAssert.AreEqual(-4, longValue, "Expected long value of -4");

        longValue = ReadLong(s);
        ClassicAssert.AreEqual(longValue, zed.ReadLong(), "Read/stream mismatch");
        ClassicAssert.AreEqual(-1, longValue, "Expected long value of -1");

        longValue = ReadLong(s);
        ClassicAssert.AreEqual(longValue, zed.ReadLong(), "Read/stream mismatch");
        ClassicAssert.AreEqual(long.MaxValue, longValue, "Expected long value of MaxValue");

        longValue = ReadLong(s);
        ClassicAssert.AreEqual(longValue, zed.ReadLong(), "Read/stream mismatch");
        ClassicAssert.AreEqual(long.MinValue, longValue, "Expected long value of MinValue");

        longValue = ReadLong(s);
        ClassicAssert.AreEqual(longValue, zed.ReadLong(), "Read/stream mismatch");
        ClassicAssert.AreEqual(0x123456789abcdef0, longValue, "Expected long value of MinValue");

        longValue = ReadLong(s);
        ClassicAssert.AreEqual(longValue, zed.ReadLong(), "Read/stream mismatch");
        ClassicAssert.AreEqual(unchecked((long)0xFEDCBA9876543210), longValue, "Expected long value of MinValue");
    }

    [Test]
    [Category("Zip")]
    public void UnreadCountValid()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        ClassicAssert.AreEqual(4, zed.Length, "Length should be 4");
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");
        ClassicAssert.AreEqual(0, zed.UnreadCount);

        // seven bytes
        zed = new ZipExtraData(new byte[] { 1, 0, 7, 0, 1, 2, 3, 4, 5, 6, 7 });
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        for (var i = 0; i < 7; ++i)
        {
            ClassicAssert.AreEqual(7 - i, zed.UnreadCount);
            zed.ReadByte();
        }

        zed.ReadByte();
        ClassicAssert.AreEqual(0, zed.UnreadCount);
    }

    [Test]
    [Category("Zip")]
    public void Skipping()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 7, 0, 1, 2, 3, 4, 5, 6, 7 });
        ClassicAssert.AreEqual(11, zed.Length, "Length should be 11");
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        ClassicAssert.AreEqual(7, zed.UnreadCount);
        ClassicAssert.AreEqual(4, zed.CurrentReadIndex);

        zed.ReadByte();
        ClassicAssert.AreEqual(6, zed.UnreadCount);
        ClassicAssert.AreEqual(5, zed.CurrentReadIndex);

        zed.Skip(1);
        ClassicAssert.AreEqual(5, zed.UnreadCount);
        ClassicAssert.AreEqual(6, zed.CurrentReadIndex);

        zed.Skip(-1);
        ClassicAssert.AreEqual(6, zed.UnreadCount);
        ClassicAssert.AreEqual(5, zed.CurrentReadIndex);

        zed.Skip(6);
        ClassicAssert.AreEqual(0, zed.UnreadCount);
        ClassicAssert.AreEqual(11, zed.CurrentReadIndex);

        var exceptionCaught = false;

        try
        {
            zed.Skip(1);
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Should fail to skip past end");

        ClassicAssert.AreEqual(0, zed.UnreadCount);
        ClassicAssert.AreEqual(11, zed.CurrentReadIndex);

        zed.Skip(-7);
        ClassicAssert.AreEqual(7, zed.UnreadCount);
        ClassicAssert.AreEqual(4, zed.CurrentReadIndex);

        exceptionCaught = false;
        try
        {
            zed.Skip(-1);
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Should fail to skip before beginning");
    }

    [Test]
    [Category("Zip")]
    public void ReadOverrunLong()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        ClassicAssert.AreEqual(4, zed.Length, "Length should be 4");
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        // Empty Tag
        var exceptionCaught = false;
        try
        {
            zed.ReadLong();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Expected EOS exception");

        // seven bytes
        zed = new ZipExtraData(new byte[] { 1, 0, 7, 0, 1, 2, 3, 4, 5, 6, 7 });
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        exceptionCaught = false;
        try
        {
            zed.ReadLong();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Expected EOS exception");

        zed = new ZipExtraData(new byte[] { 1, 0, 15, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 });
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        zed.ReadLong();

        exceptionCaught = false;
        try
        {
            zed.ReadLong();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Expected EOS exception");
    }

    [Test]
    [Category("Zip")]
    public void ReadOverrunInt()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        ClassicAssert.AreEqual(4, zed.Length, "Length should be 4");
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        // Empty Tag
        var exceptionCaught = false;
        try
        {
            zed.ReadInt();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Expected EOS exception");

        // three bytes
        zed = new ZipExtraData(new byte[] { 1, 0, 3, 0, 1, 2, 3 });
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        exceptionCaught = false;
        try
        {
            zed.ReadInt();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Expected EOS exception");

        zed = new ZipExtraData(new byte[] { 1, 0, 7, 0, 1, 2, 3, 4, 5, 6, 7 });
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        zed.ReadInt();

        exceptionCaught = false;
        try
        {
            zed.ReadInt();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Expected EOS exception");
    }

    [Test]
    [Category("Zip")]
    public void ReadOverrunShort()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        ClassicAssert.AreEqual(4, zed.Length, "Length should be 4");
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        // Empty Tag
        var exceptionCaught = false;
        try
        {
            zed.ReadShort();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Expected EOS exception");

        // Single byte
        zed = new ZipExtraData(new byte[] { 1, 0, 1, 0, 1 });
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        exceptionCaught = false;
        try
        {
            zed.ReadShort();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Expected EOS exception");

        zed = new ZipExtraData(new byte[] { 1, 0, 2, 0, 1, 2 });
        ClassicAssert.IsTrue(zed.Find(1), "Should find tag 1");

        zed.ReadShort();

        exceptionCaught = false;
        try
        {
            zed.ReadShort();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        ClassicAssert.IsTrue(exceptionCaught, "Expected EOS exception");
    }

    [Test]
    [Category("Zip")]
    public void TaggedDataHandling()
    {
        var tagData = new NTTaggedData();
        var modTime = tagData.LastModificationTime;
        var rawData = tagData.GetData();
        tagData.LastModificationTime = tagData.LastModificationTime + TimeSpan.FromSeconds(40);
        ClassicAssert.AreNotEqual(tagData.LastModificationTime, modTime);
        tagData.SetData(rawData, 0, rawData.Length);
        ClassicAssert.AreEqual(10, tagData.TagID, "TagID mismatch");
        ClassicAssert.AreEqual(modTime, tagData.LastModificationTime, "NT Mod time incorrect");

        tagData.CreateTime = DateTime.FromFileTimeUtc(0);
        tagData.LastAccessTime = new DateTime(9999, 12, 31, 23, 59, 59);
        rawData = tagData.GetData();

        var unixData = new ExtendedUnixData();
        modTime = unixData.ModificationTime;
        unixData.ModificationTime = modTime; // Ensure flag is set.

        rawData = unixData.GetData();
        unixData.ModificationTime += TimeSpan.FromSeconds(100);
        ClassicAssert.AreNotEqual(unixData.ModificationTime, modTime);
        unixData.SetData(rawData, 0, rawData.Length);
        ClassicAssert.AreEqual(0x5455, unixData.TagID, "TagID mismatch");
        ClassicAssert.AreEqual(modTime, unixData.ModificationTime, "Unix mod time incorrect");
    }
}