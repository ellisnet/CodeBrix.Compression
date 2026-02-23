using System;

// ReSharper disable InconsistentNaming

namespace CodeBrix.Compression.Zip;

/// <summary>
/// The method of encrypting entries when creating zip archives.
/// </summary>
public enum ZipEncryptionMethod
{
    /// <summary>
    /// No encryption will be used.
    /// </summary>
    None,

    /// <summary>
    /// Encrypt entries with ZipCrypto.
    /// </summary>
    [Obsolete("ZipCrypto is considered weak and should not be used for secure encryption. Consider using AES128 or AES256 instead.")]
    ZipCrypto,

    /// <summary>
    /// Encrypt entries with AES 128.
    /// </summary>
    AES128,

    /// <summary>
    /// Encrypt entries with AES 256.
    /// </summary>
    AES256
}
