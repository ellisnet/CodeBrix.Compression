// The decoding algorithm in this file is a C# port of "blast" version 1.3 -
// Mark Adler's public reference decoder for the PKWARE Data Compression
// Library (DCL) compressed format (zlib contrib/blast/blast.c, blast.h,
// Copyright (C) 2003, 2012, 2013 Mark Adler, zlib license) - see the
// THIRD-PARTY-NOTICES.txt file at the root of this repository.

using System;
using System.IO;

namespace CodeBrix.Compression.Dcl;

/// <summary>
/// This filter stream is used to decompress data compressed in the
/// PKWARE Data Compression Library (DCL) "imploded" format, as produced by the
/// implode() function of that library. This format was widely used by
/// MS-DOS-era installers and self-extracting distributions of the early 1990s.
///
/// NOTE: this format is NOT the same as the "Imploded" (method 6) compression
/// method of the Zip file format, and it is NOT produced by PKZIP; it is the
/// stream format of PKWARE's separately licensed Data Compression Library.
///
/// A DCL stream starts with two header bytes: the first is 0 if literals are
/// stored uncoded, or 1 if they are Huffman coded; the second is 4, 5, or 6 -
/// the base-2 logarithm of the dictionary size minus six (1024, 2048, or 4096
/// bytes). The remainder is a bit stream of literals and length/distance
/// copies, terminated by an end-of-stream code.
/// </summary>
/// <example> This sample shows how to decompress a DCL-imploded file
/// <code>
/// using System;
/// using System.IO;
///
/// using CodeBrix.Compression.Dcl;
///
/// class MainClass
/// {
/// 	public static void Main(string[] args)
/// 	{
///			using (Stream inStream = new DclInputStream(File.OpenRead(args[0])))
///			using (FileStream outStream = File.Create(args[1])) {
///				inStream.CopyTo(outStream);
/// 		}
/// 	}
/// }
/// </code>
/// </example>
public class DclInputStream : Stream
{
    private const int MaxBits = 13;               // maximum Huffman code length
    private const int WindowSize = 4096;          // maximum dictionary size
    private const int WindowMask = WindowSize - 1;
    private const int EndOfStreamLength = 519;    // length symbol value marking end of stream

    #region Huffman decoding tables

    private sealed class HuffmanTable
    {
        // Count[1..MaxBits] is the number of symbols of each code length;
        // Symbol[] holds the symbol values in canonical order.
        public readonly short[] Count = new short[MaxBits + 1];
        public readonly short[] Symbol;

        // Builds the decoding table from a compact list of repeated code lengths,
        // where each byte holds a repeat count (high four bits + 1) and a code
        // length (low four bits).
        public HuffmanTable(byte[] compactLengths, int symbolCount)
        {
            Symbol = new short[symbolCount];

            var length = new short[256];
            int symbol = 0;
            foreach (byte rep in compactLengths)
            {
                int repeat = (rep >> 4) + 1;
                var codeLength = (short)(rep & 15);
                while (repeat-- > 0)
                {
                    length[symbol++] = codeLength;
                }
            }
            int total = symbol;

            for (symbol = 0; symbol < total; symbol++)
            {
                Count[length[symbol]]++;
            }

            // Reject an over-subscribed set of code lengths (cannot happen with
            // the fixed DCL tables below, but guards future modification).
            int possible = 1;
            for (int len = 1; len <= MaxBits; len++)
            {
                possible <<= 1;
                possible -= Count[len];
                if (possible < 0)
                {
                    throw new DclException("Over-subscribed Huffman code length set in DCL decoding table.");
                }
            }

            var offsets = new short[MaxBits + 1];
            for (int len = 1; len < MaxBits; len++)
            {
                offsets[len + 1] = (short)(offsets[len] + Count[len]);
            }

            for (symbol = 0; symbol < total; symbol++)
            {
                if (length[symbol] != 0)
                {
                    Symbol[offsets[length[symbol]]++] = (short)symbol;
                }
            }
        }
    }

    // Compact bit lengths of the fixed DCL literal codes (256 symbols).
    private static readonly byte[] LiteralCodeLengths =
    {
        11, 124, 8, 7, 28, 7, 188, 13, 76, 4, 10, 8, 12, 10, 12, 10, 8, 23, 8,
        9, 7, 6, 7, 8, 7, 6, 55, 8, 23, 24, 12, 11, 7, 9, 11, 12, 6, 7, 22, 5,
        7, 24, 6, 11, 9, 6, 7, 22, 7, 11, 38, 7, 9, 8, 25, 11, 8, 11, 9, 12,
        8, 12, 5, 38, 5, 38, 5, 11, 7, 5, 6, 21, 6, 10, 53, 8, 7, 24, 10, 27,
        44, 253, 253, 253, 252, 252, 252, 13, 12, 45, 12, 45, 12, 61, 12, 45,
        44, 173
    };

    // Compact bit lengths of the fixed DCL length codes (16 symbols).
    private static readonly byte[] LengthCodeLengths = { 2, 35, 36, 53, 38, 23 };

    // Compact bit lengths of the fixed DCL distance codes (64 symbols).
    private static readonly byte[] DistanceCodeLengths = { 2, 20, 53, 230, 247, 151, 248 };

    // Base values and extra-bit counts for the 16 length symbols.
    private static readonly short[] LengthBase = { 3, 2, 4, 5, 6, 7, 8, 9, 10, 12, 16, 24, 40, 72, 136, 264 };
    private static readonly byte[] LengthExtraBits = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 };

    private static readonly HuffmanTable LiteralCode = new HuffmanTable(LiteralCodeLengths, 256);
    private static readonly HuffmanTable LengthCode = new HuffmanTable(LengthCodeLengths, 16);
    private static readonly HuffmanTable DistanceCode = new HuffmanTable(DistanceCodeLengths, 64);

    #endregion Huffman decoding tables

    #region Instance Fields

    private readonly Stream baseInputStream;

    private readonly byte[] inputBuffer = new byte[4096];
    private int inputPos;
    private int inputEnd;

    private int bitBuffer;
    private int bitCount;

    // Sliding window of previously decompressed data, used as the copy dictionary.
    private readonly byte[] window = new byte[WindowSize];
    private int windowNext;
    private long totalWritten;

    private bool headerRead;
    private bool codedLiterals;
    private int dictBits;

    // In-progress length/distance copy carried across Read calls.
    private int copyDistance;
    private int copyRemaining;

    private bool endOfStream;
    private bool isClosed;

    #endregion Instance Fields

    /// <summary>
    /// Gets or sets a flag indicating ownership of underlying stream.
    /// When the flag is true <see cref="Stream.Dispose()" /> will close the underlying stream also.
    /// </summary>
    /// <remarks>The default value is true.</remarks>
    public bool IsStreamOwner { get; set; } = true;

    /// <summary>
    /// Creates a DclInputStream
    /// </summary>
    /// <param name="baseInputStream">
    /// The stream to read compressed data from (baseInputStream DCL "imploded" format)
    /// </param>
    public DclInputStream(Stream baseInputStream)
    {
        this.baseInputStream = baseInputStream ?? throw new ArgumentNullException(nameof(baseInputStream));
    }

    /// <summary>
    /// Reads decompressed data into the provided buffer byte array
    /// </summary>
    /// <param name="buffer">The array to read and decompress data into</param>
    /// <param name="offset">The offset indicating where the data should be placed</param>
    /// <param name="count">The number of bytes to decompress</param>
    /// <returns>The number of bytes read. Zero signals the end of stream</returns>
    /// <exception cref="DclException">The compressed data is malformed or truncated</exception>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(offset < 0 || offset > buffer.Length ? nameof(offset) : nameof(count));
        }

        if (!headerRead)
        {
            ReadHeader();
        }

        int written = 0;
        while (written < count)
        {
            if (copyRemaining > 0)
            {
                // Continue (or start delivering) a length/distance copy. Copying
                // byte-by-byte through the window handles overlapped copies
                // (length greater than distance) correctly.
                byte value = window[(windowNext - copyDistance) & WindowMask];
                copyRemaining--;
                buffer[offset + written++] = EmitByte(value);
                continue;
            }

            if (endOfStream)
            {
                break;
            }

            if (ReadBits(1) == 1)
            {
                int symbol = DecodeSymbol(LengthCode);
                int length = LengthBase[symbol] + ReadBits(LengthExtraBits[symbol]);
                if (length == EndOfStreamLength)
                {
                    endOfStream = true;
                    continue;
                }

                // A length of 2 always uses 2 extra distance bits; all other
                // lengths use the dictionary-size number of extra bits.
                int distanceBits = (length == 2) ? 2 : dictBits;
                int distance = (DecodeSymbol(DistanceCode) << distanceBits) + ReadBits(distanceBits) + 1;
                if (distance > totalWritten)
                {
                    throw new DclException("DCL copy distance points before the start of the output data.");
                }

                copyDistance = distance;
                copyRemaining = length;
            }
            else
            {
                int literal = codedLiterals ? DecodeSymbol(LiteralCode) : ReadBits(8);
                buffer[offset + written++] = EmitByte((byte)literal);
            }
        }

        return written;
    }

    #region Decoding

    // Reads and validates the two-byte DCL stream header.
    private void ReadHeader()
    {
        int literalFlag = ReadBits(8);
        if (literalFlag > 1)
        {
            throw new DclException($"Invalid DCL literal mode flag {literalFlag} - expected 0 (uncoded) or 1 (coded).");
        }

        codedLiterals = (literalFlag == 1);

        dictBits = ReadBits(8);
        if (dictBits < 4 || dictBits > 6)
        {
            throw new DclException($"Invalid DCL dictionary size code {dictBits} - expected 4, 5 or 6.");
        }

        headerRead = true;
    }

    // Returns the next raw byte of compressed input, bypassing the bit buffer.
    private int ReadRawByte()
    {
        if (inputPos >= inputEnd)
        {
            inputEnd = baseInputStream.Read(inputBuffer, 0, inputBuffer.Length);
            inputPos = 0;
            if (inputEnd <= 0)
            {
                throw new DclException("Unexpected end of DCL compressed stream.");
            }
        }

        return inputBuffer[inputPos++];
    }

    // Returns the requested number of bits from the input stream. Bits are
    // stored in bytes from the least significant bit to the most significant
    // bit, so bits are dropped from the bottom of the bit buffer and new bytes
    // are appended to the top.
    private int ReadBits(int need)
    {
        int val = bitBuffer;
        while (bitCount < need)
        {
            val |= ReadRawByte() << bitCount;
            bitCount += 8;
        }

        bitBuffer = val >> need;
        bitCount -= need;
        return val & ((1 << need) - 1);
    }

    // Decodes one symbol using the given canonical Huffman table. The codes as
    // stored in the compressed data are bit-reversed AND inverted relative to a
    // simple integer ordering of codes of the same lengths, so bits are pulled
    // one at a time and inverted to permit simple integer range comparisons.
    private int DecodeSymbol(HuffmanTable table)
    {
        int localBits = bitBuffer;
        int available = bitCount;
        int code = 0;
        int first = 0;
        int index = 0;
        int len = 1;
        int nextLength = 1;

        while (true)
        {
            while (available-- > 0)
            {
                code |= (localBits & 1) ^ 1;
                localBits >>= 1;
                int count = table.Count[nextLength++];
                if (code < first + count)
                {
                    // Found the symbol: consume len bits (whole bytes were
                    // loaded, so the remainder count is modulo 8).
                    bitBuffer = localBits;
                    bitCount = (bitCount - len) & 7;
                    return table.Symbol[index + (code - first)];
                }

                index += count;
                first += count;
                first <<= 1;
                code <<= 1;
                len++;
            }

            available = (MaxBits + 1) - len;
            if (available == 0)
            {
                break;
            }

            localBits = ReadRawByte();
            if (available > 8)
            {
                available = 8;
            }
        }

        throw new DclException("Invalid Huffman code in DCL compressed stream.");
    }

    // Records a decompressed byte in the sliding window and returns it.
    private byte EmitByte(byte value)
    {
        window[windowNext] = value;
        windowNext = (windowNext + 1) & WindowMask;
        totalWritten++;
        return value;
    }

    #endregion Decoding

    #region Stream Overrides

    /// <summary>
    /// Gets a value indicating whether the current stream supports reading
    /// </summary>
    public override bool CanRead => baseInputStream.CanRead;

    /// <summary>
    /// Gets a value of false indicating seeking is not supported for this stream.
    /// </summary>
    public override bool CanSeek => false;

    /// <summary>
    /// Gets a value of false indicating that this stream is not writeable.
    /// </summary>
    public override bool CanWrite => false;

    /// <summary>
    /// A value representing the length of the stream in bytes.
    /// </summary>
    public override long Length => throw new NotSupportedException("DclInputStream Length is not supported");

    /// <summary>
    /// The current position within the stream.
    /// Throws a NotSupportedException when attempting to set the position
    /// </summary>
    /// <exception cref="NotSupportedException">Attempting to set the position</exception>
    public override long Position
    {
        get => baseInputStream.Position;
        set => throw new NotSupportedException("DclInputStream Position not supported");
    }

    /// <summary>
    /// Flushes the baseInputStream
    /// </summary>
    public override void Flush() => baseInputStream.Flush();

    /// <summary>
    /// Sets the position within the current stream
    /// Always throws a NotSupportedException
    /// </summary>
    /// <param name="offset">The relative offset to seek to.</param>
    /// <param name="origin">The <see cref="SeekOrigin"/> defining where to seek from.</param>
    /// <returns>The new position in the stream.</returns>
    /// <exception cref="NotSupportedException">Any access</exception>
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException("DclInputStream Seek not supported");

    /// <summary>
    /// Set the length of the current stream
    /// Always throws a NotSupportedException
    /// </summary>
    /// <param name="value">The new length value for the stream.</param>
    /// <exception cref="NotSupportedException">Any access</exception>
    public override void SetLength(long value) =>
        throw new NotSupportedException("DclInputStream SetLength not supported");

    /// <summary>
    /// Writes a sequence of bytes to stream and advances the current position
    /// This method always throws a NotSupportedException
    /// </summary>
    /// <param name="buffer">The buffer containing data to write.</param>
    /// <param name="offset">The offset of the first byte to write.</param>
    /// <param name="count">The number of bytes to write.</param>
    /// <exception cref="NotSupportedException">Any access</exception>
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("DclInputStream Write not supported");

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="Stream"/> and optionally
    /// releases the managed resources, closing the underlying stream when this
    /// instance <see cref="IsStreamOwner">owns</see> it.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources;
    /// false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (!isClosed)
        {
            isClosed = true;

            if (disposing && IsStreamOwner)
            {
                baseInputStream.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    #endregion Stream Overrides
}
