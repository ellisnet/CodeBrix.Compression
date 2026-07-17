using System;
using System.IO;

namespace CodeBrix.Compression.Dcl;

/// <summary>
/// An example class to demonstrate decompression of DCL (PKWARE Data Compression
/// Library "imploded" format) streams.
/// </summary>
public static class Dcl
{
    /// <summary>
    /// Decompress the <paramref name="inStream">input</paramref> writing
    /// uncompressed data to the <paramref name="outStream">output stream</paramref>
    /// </summary>
    /// <param name="inStream">The readable stream containing data to decompress.</param>
    /// <param name="outStream">The output stream to receive the decompressed data.</param>
    /// <param name="isStreamOwner">Both streams are closed on completion if true.</param>
    /// <exception cref="ArgumentNullException">Input or output stream is null</exception>
    /// <exception cref="DclException">The compressed data is malformed or truncated</exception>
    public static void Decompress(Stream inStream, Stream outStream, bool isStreamOwner)
    {
        if (inStream == null)
        {
            throw new ArgumentNullException(nameof(inStream), "Input stream is null");
        }

        if (outStream == null)
        {
            throw new ArgumentNullException(nameof(outStream), "Output stream is null");
        }

        try
        {
            using var dclInput = new DclInputStream(inStream);
            dclInput.IsStreamOwner = isStreamOwner;
            Core.StreamUtils.Copy(dclInput, outStream, new byte[4096]);
        }
        finally
        {
            if (isStreamOwner)
            {
                // inStream is closed by the DclInputStream if stream owner
                outStream.Dispose();
            }
        }
    }
}
