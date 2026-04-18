using System;
using System.IO;
using System.Security.Cryptography;
using CodeBrix.Compression.Encryption;
using Xunit;

namespace CodeBrix.Compression.Tests.Encryption;

[Trait("Category", "Encryption")]
public class EncryptionTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(24)]
    [InlineData(64)]
    public void ZipAESTransform_ThrowsOnInvalidBlockSize(int blockSize)
    {
        var salt = new byte[blockSize / 2 > 0 ? blockSize / 2 : 1];

        Assert.Throws<Exception>(() => new ZipAESTransform("password", salt, blockSize, writeMode: true));
    }

    [Fact]
    public void ZipAESTransform_ThrowsOnInvalidSaltLength()
    {
        // AES-128 (blockSize=16) requires salt of length 8
        var wrongSalt = new byte[4];

        Assert.Throws<Exception>(() => new ZipAESTransform("password", wrongSalt, 16, writeMode: true));
    }

    [Theory]
    [InlineData(16, 8)]
    [InlineData(32, 16)]
    public void ZipAESTransform_SucceedsWithValidParameters(int blockSize, int saltLength)
    {
        var salt = new byte[saltLength];
        RandomNumberGenerator.Fill(salt);

        ZipAESTransform transform = null;
        var ex = Record.Exception(() => transform = new ZipAESTransform("password", salt, blockSize, writeMode: true));
        Assert.Null(ex);

        Assert.NotNull(transform);
        Assert.NotNull(transform.PwdVerifier);
        Assert.Equal(2, transform.PwdVerifier.Length);

        transform.Dispose();
    }

    [Fact]
    public void ZipAESStream_ThrowsWhenConstructedInWriteMode()
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);

        using var transform = new ZipAESTransform("password", salt, 32, writeMode: true);
        using var ms = new MemoryStream();

        Assert.Throws<Exception>(() => new ZipAESStream(ms, transform, CryptoStreamMode.Write));
    }
}
