using System;
using System.IO;
using System.Security.Cryptography;
using CodeBrix.Compression.Encryption;
using NUnit.Framework;

namespace CodeBrix.Compression.Tests.Encryption;

[TestFixture]
public class EncryptionTests
{
    [Test]
    [Category("Encryption")]
    [TestCase(8)]
    [TestCase(24)]
    [TestCase(64)]
    public void ZipAESTransform_ThrowsOnInvalidBlockSize(int blockSize)
    {
        var salt = new byte[blockSize / 2 > 0 ? blockSize / 2 : 1];

        Assert.Throws<Exception>(() => new ZipAESTransform("password", salt, blockSize, writeMode: true));
    }

    [Test]
    [Category("Encryption")]
    public void ZipAESTransform_ThrowsOnInvalidSaltLength()
    {
        // AES-128 (blockSize=16) requires salt of length 8
        var wrongSalt = new byte[4];

        Assert.Throws<Exception>(() => new ZipAESTransform("password", wrongSalt, 16, writeMode: true));
    }

    [Test]
    [Category("Encryption")]
    [TestCase(16, 8)]
    [TestCase(32, 16)]
    public void ZipAESTransform_SucceedsWithValidParameters(int blockSize, int saltLength)
    {
        var salt = new byte[saltLength];
        RandomNumberGenerator.Fill(salt);

        ZipAESTransform transform = null;
        Assert.DoesNotThrow(() => transform = new ZipAESTransform("password", salt, blockSize, writeMode: true));

        Assert.That(transform, Is.Not.Null);
        Assert.That(transform.PwdVerifier, Is.Not.Null);
        Assert.That(transform.PwdVerifier.Length, Is.EqualTo(2));

        transform.Dispose();
    }

    [Test]
    [Category("Encryption")]
    public void ZipAESStream_ThrowsWhenConstructedInWriteMode()
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);

        using var transform = new ZipAESTransform("password", salt, 32, writeMode: true);
        using var ms = new MemoryStream();

        Assert.Throws<Exception>(() => new ZipAESStream(ms, transform, CryptoStreamMode.Write));
    }
}
