using CodeBrix.Compression.Zip;
using System;
using System.IO;
using Xunit;

namespace CodeBrix.Compression.Tests.Zip;

[Trait("Category", "Zip")]
public class ZipExtraDataHandling : ZipBase
{
    /// <summary>
    /// Extra data for separate entries should be unique to that entry
    /// </summary>
    [Fact]
    public void IsDataUnique()
    {
        var a = new ZipEntry("Basil");
        var extra = new byte[4];
        extra[0] = 27;
        a.ExtraData = extra;

        var b = (ZipEntry)a.Clone();
        b.ExtraData[0] = 89;
        Assert.True(b.ExtraData[0] != a.ExtraData[0], "Extra data not unique " + b.ExtraData[0] + " " + a.ExtraData[0]);

        var c = (ZipEntry)a.Clone();
        c.ExtraData[0] = 45;
        Assert.True(a.ExtraData[0] != c.ExtraData[0], "Extra data not unique " + a.ExtraData[0] + " " + c.ExtraData[0]);
    }

    [Fact]
    public void ExceedSize()
    {
        var zed = new ZipExtraData();
        var buffer = new byte[65506];
        zed.AddEntry(1, buffer);
        Assert.Equal(65510, zed.Length);
        zed.AddEntry(2, new byte[21]);
        Assert.Equal(65535, zed.Length);

        var caught = false;
        try
        {
            zed.AddEntry(3, null);
        }
        catch
        {
            caught = true;
        }

        Assert.True(caught, "Expected an exception when max size exceeded");
        Assert.Equal(65535, zed.Length);

        zed.Delete(2);
        Assert.Equal(65510, zed.Length);

        caught = false;
        try
        {
            zed.AddEntry(2, new byte[22]);
        }
        catch
        {
            caught = true;
        }
        Assert.True(caught, "Expected an exception when max size exceeded");
        Assert.Equal(65510, zed.Length);
    }

    [Fact]
    public void Deleting()
    {
        var zed = new ZipExtraData();
        Assert.Equal(0, zed.Length);

        // Tag 1 Totoal length 10
        zed.AddEntry(1, new byte[] { 10, 11, 12, 13, 14, 15 });
        Assert.Equal(10, zed.Length);
        Assert.Equal(10, zed.GetEntryData().Length);

        // Tag 2 total length  9
        zed.AddEntry(2, new byte[] { 20, 21, 22, 23, 24 });
        Assert.Equal(19, zed.Length);
        Assert.Equal(19, zed.GetEntryData().Length);

        // Tag 3 Total Length 6
        zed.AddEntry(3, new byte[] { 30, 31 });
        Assert.Equal(25, zed.Length);
        Assert.Equal(25, zed.GetEntryData().Length);

        zed.Delete(2);
        Assert.Equal(16, zed.Length);
        Assert.Equal(16, zed.GetEntryData().Length);

        // Tag 2 total length  9
        zed.AddEntry(2, new byte[] { 20, 21, 22, 23, 24 });
        Assert.Equal(25, zed.Length);
        Assert.Equal(25, zed.GetEntryData().Length);

        zed.AddEntry(3, null);
        Assert.Equal(23, zed.Length);
        Assert.Equal(23, zed.GetEntryData().Length);
    }

    [Fact]
    public void BasicOperations()
    {
        var zed = new ZipExtraData(null);
        Assert.Equal(0, zed.Length);

        zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        Assert.Equal(4, zed.Length);

        var zed2 = new ZipExtraData();
        Assert.Equal(0, zed2.Length);

        zed2.AddEntry(1, new byte[] { });

        var data = zed.GetEntryData();
        for (var i = 0; i < data.Length; ++i)
        {
            Assert.Equal(zed2.GetEntryData()[i], data[i]);
        }

        Assert.Equal(4, zed2.Length);

        var findResult = zed.Find(2);
        Assert.False(findResult, "A - Shouldnt find tag 2");

        findResult = zed.Find(1);
        Assert.True(findResult, "A - Should find tag 1");
        Assert.Equal(0, zed.ValueLength);
        Assert.Equal(-1, zed.ReadByte());
        Assert.Equal(0, zed.GetStreamForTag(1).Length);

        zed = new ZipExtraData(new byte[] { 1, 0, 3, 0, 1, 2, 3 });
        Assert.Equal(7, zed.Length);

        findResult = zed.Find(1);
        Assert.True(findResult, "B - Should find tag 1");
        Assert.Equal(3, zed.ValueLength);
        for (var i = 1; i <= 3; ++i)
        {
            Assert.Equal(i, zed.ReadByte());
        }
        Assert.Equal(-1, zed.ReadByte());

        var s = zed.GetStreamForTag(1);
        Assert.Equal(3, s.Length);
        for (var i = 1; i <= 3; ++i)
        {
            Assert.Equal(i, s.ReadByte());
        }
        Assert.Equal(-1, s.ReadByte());

        zed = new ZipExtraData(new byte[] { 1, 0, 3, 0, 1, 2, 3, 2, 0, 1, 0, 56 });
        Assert.Equal(12, zed.Length);

        findResult = zed.Find(1);
        Assert.True(findResult, "C.1 - Should find tag 1");
        Assert.Equal(3, zed.ValueLength);
        for (var i = 1; i <= 3; ++i)
        {
            Assert.Equal(i, zed.ReadByte());
        }
        Assert.Equal(-1, zed.ReadByte());

        findResult = zed.Find(2);
        Assert.True(findResult, "C.2 - Should find tag 2");
        Assert.Equal(1, zed.ValueLength);
        Assert.Equal(56, zed.ReadByte());
        Assert.Equal(-1, zed.ReadByte());

        s = zed.GetStreamForTag(2);
        Assert.Equal(1, s.Length);
        Assert.Equal(56, s.ReadByte());
        Assert.Equal(-1, s.ReadByte());

        zed = new ZipExtraData();
        zed.AddEntry(7, new byte[] { 33, 44, 55 });
        findResult = zed.Find(7);
        Assert.True(findResult, "Add.1 should find new tag");
        Assert.Equal(3, zed.ValueLength);
        Assert.Equal(33, zed.ReadByte());
        Assert.Equal(44, zed.ReadByte());
        Assert.Equal(55, zed.ReadByte());
        Assert.Equal(-1, zed.ReadByte());

        zed.AddEntry(7, null);
        findResult = zed.Find(7);
        Assert.True(findResult, "Add.2 should find new tag");
        Assert.Equal(0, zed.ValueLength);

        zed.StartNewEntry();
        zed.AddData(0xae);
        zed.AddNewEntry(55);

        findResult = zed.Find(55);
        Assert.True(findResult, "Add.3 should find new tag");
        Assert.Equal(1, zed.ValueLength);
        Assert.Equal(0xae, zed.ReadByte());
        Assert.Equal(-1, zed.ReadByte());

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
        Assert.Equal(longValue, zed.ReadLong());
        Assert.Equal(0, longValue);

        longValue = ReadLong(s);
        Assert.Equal(longValue, zed.ReadLong());
        Assert.Equal(-4, longValue);

        longValue = ReadLong(s);
        Assert.Equal(longValue, zed.ReadLong());
        Assert.Equal(-1, longValue);

        longValue = ReadLong(s);
        Assert.Equal(longValue, zed.ReadLong());
        Assert.Equal(long.MaxValue, longValue);

        longValue = ReadLong(s);
        Assert.Equal(longValue, zed.ReadLong());
        Assert.Equal(long.MinValue, longValue);

        longValue = ReadLong(s);
        Assert.Equal(longValue, zed.ReadLong());
        Assert.Equal(0x123456789abcdef0, longValue);

        longValue = ReadLong(s);
        Assert.Equal(longValue, zed.ReadLong());
        Assert.Equal(unchecked((long)0xFEDCBA9876543210), longValue);
    }

    [Fact]
    public void UnreadCountValid()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        Assert.Equal(4, zed.Length);
        Assert.True(zed.Find(1), "Should find tag 1");
        Assert.Equal(0, zed.UnreadCount);

        // seven bytes
        zed = new ZipExtraData(new byte[] { 1, 0, 7, 0, 1, 2, 3, 4, 5, 6, 7 });
        Assert.True(zed.Find(1), "Should find tag 1");

        for (var i = 0; i < 7; ++i)
        {
            Assert.Equal(7 - i, zed.UnreadCount);
            zed.ReadByte();
        }

        zed.ReadByte();
        Assert.Equal(0, zed.UnreadCount);
    }

    [Fact]
    public void Skipping()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 7, 0, 1, 2, 3, 4, 5, 6, 7 });
        Assert.Equal(11, zed.Length);
        Assert.True(zed.Find(1), "Should find tag 1");

        Assert.Equal(7, zed.UnreadCount);
        Assert.Equal(4, zed.CurrentReadIndex);

        zed.ReadByte();
        Assert.Equal(6, zed.UnreadCount);
        Assert.Equal(5, zed.CurrentReadIndex);

        zed.Skip(1);
        Assert.Equal(5, zed.UnreadCount);
        Assert.Equal(6, zed.CurrentReadIndex);

        zed.Skip(-1);
        Assert.Equal(6, zed.UnreadCount);
        Assert.Equal(5, zed.CurrentReadIndex);

        zed.Skip(6);
        Assert.Equal(0, zed.UnreadCount);
        Assert.Equal(11, zed.CurrentReadIndex);

        var exceptionCaught = false;

        try
        {
            zed.Skip(1);
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        Assert.True(exceptionCaught, "Should fail to skip past end");

        Assert.Equal(0, zed.UnreadCount);
        Assert.Equal(11, zed.CurrentReadIndex);

        zed.Skip(-7);
        Assert.Equal(7, zed.UnreadCount);
        Assert.Equal(4, zed.CurrentReadIndex);

        exceptionCaught = false;
        try
        {
            zed.Skip(-1);
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        Assert.True(exceptionCaught, "Should fail to skip before beginning");
    }

    [Fact]
    public void ReadOverrunLong()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        Assert.Equal(4, zed.Length);
        Assert.True(zed.Find(1), "Should find tag 1");

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
        Assert.True(exceptionCaught, "Expected EOS exception");

        // seven bytes
        zed = new ZipExtraData(new byte[] { 1, 0, 7, 0, 1, 2, 3, 4, 5, 6, 7 });
        Assert.True(zed.Find(1), "Should find tag 1");

        exceptionCaught = false;
        try
        {
            zed.ReadLong();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        Assert.True(exceptionCaught, "Expected EOS exception");

        zed = new ZipExtraData(new byte[] { 1, 0, 15, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 });
        Assert.True(zed.Find(1), "Should find tag 1");

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
        Assert.True(exceptionCaught, "Expected EOS exception");
    }

    [Fact]
    public void ReadOverrunInt()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        Assert.Equal(4, zed.Length);
        Assert.True(zed.Find(1), "Should find tag 1");

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
        Assert.True(exceptionCaught, "Expected EOS exception");

        // three bytes
        zed = new ZipExtraData(new byte[] { 1, 0, 3, 0, 1, 2, 3 });
        Assert.True(zed.Find(1), "Should find tag 1");

        exceptionCaught = false;
        try
        {
            zed.ReadInt();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        Assert.True(exceptionCaught, "Expected EOS exception");

        zed = new ZipExtraData(new byte[] { 1, 0, 7, 0, 1, 2, 3, 4, 5, 6, 7 });
        Assert.True(zed.Find(1), "Should find tag 1");

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
        Assert.True(exceptionCaught, "Expected EOS exception");
    }

    [Fact]
    public void ReadOverrunShort()
    {
        var zed = new ZipExtraData(new byte[] { 1, 0, 0, 0 });
        Assert.Equal(4, zed.Length);
        Assert.True(zed.Find(1), "Should find tag 1");

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
        Assert.True(exceptionCaught, "Expected EOS exception");

        // Single byte
        zed = new ZipExtraData(new byte[] { 1, 0, 1, 0, 1 });
        Assert.True(zed.Find(1), "Should find tag 1");

        exceptionCaught = false;
        try
        {
            zed.ReadShort();
        }
        catch (ZipException)
        {
            exceptionCaught = true;
        }
        Assert.True(exceptionCaught, "Expected EOS exception");

        zed = new ZipExtraData(new byte[] { 1, 0, 2, 0, 1, 2 });
        Assert.True(zed.Find(1), "Should find tag 1");

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
        Assert.True(exceptionCaught, "Expected EOS exception");
    }

    [Fact]
    public void TaggedDataHandling()
    {
        var tagData = new NTTaggedData();
        var modTime = tagData.LastModificationTime;
        var rawData = tagData.GetData();
        tagData.LastModificationTime = tagData.LastModificationTime + TimeSpan.FromSeconds(40);
        Assert.NotEqual(tagData.LastModificationTime, modTime);
        tagData.SetData(rawData, 0, rawData.Length);
        Assert.Equal(10, tagData.TagID);
        Assert.Equal(modTime, tagData.LastModificationTime);

        tagData.CreateTime = DateTime.FromFileTimeUtc(0);
        tagData.LastAccessTime = new DateTime(9999, 12, 31, 23, 59, 59);
        rawData = tagData.GetData();

        var unixData = new ExtendedUnixData();
        modTime = unixData.ModificationTime;
        unixData.ModificationTime = modTime; // Ensure flag is set.

        rawData = unixData.GetData();
        unixData.ModificationTime += TimeSpan.FromSeconds(100);
        Assert.NotEqual(unixData.ModificationTime, modTime);
        unixData.SetData(rawData, 0, rawData.Length);
        Assert.Equal(0x5455, unixData.TagID);
        Assert.Equal(modTime, unixData.ModificationTime);
    }
}
