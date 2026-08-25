================================================================================
AGENT-README: CodeBrix.Compression
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Compression.MitLicenseForever NuGet package
================================================================================

OVERVIEW
========
CodeBrix.Compression is a .NET library for creating, reading, updating, and
extracting compressed archives in multiple formats: Zip, GZip, Tar, and BZip2.
It can also decompress data in the PKWARE Data Compression Library (DCL)
"imploded" stream format used by many MS-DOS-era installers.

It supports encryption (AES-128, AES-256, ZipCrypto), Zip64 extensions for large
files, streaming operations, in-memory archive operations, and checksums.

Target framework: .NET 10 or later.

CodeBrix.Compression has ZERO external dependencies beyond .NET itself.

PROVENANCE: CodeBrix.Compression is a fork of SharpZipLib 1.4.2, plus a DCL
"implode" decoder ported from Mark Adler's "blast" reference decoder. If you are
familiar with SharpZipLib, the API surface is very similar, but ALL namespaces
use "CodeBrix.Compression" instead of "ICSharpCode.SharpZipLib". Do NOT write
the upstream namespaces, and do NOT reference both libraries from the same
project.

Source repository: https://github.com/ellisnet/CodeBrix.Compression

================================================================================

INSTALLATION
============
NuGet PackageId: CodeBrix.Compression.MitLicenseForever
NuGet dependencies: none
License: MIT (SPDX: MIT)
Requirements: .NET 10 or later. Fully managed - no native libraries, no
platform-specific code, no OS restrictions.

To add to a .NET 10+ project (always use the latest available version):

    dotnet add package CodeBrix.Compression.MitLicenseForever

Or add a PackageReference to a .csproj file, substituting the latest published
version number for {latest-version}:

    <PackageReference Include="CodeBrix.Compression.MitLicenseForever" Version="{latest-version}" />

IMPORTANT: The package name is "CodeBrix.Compression.MitLicenseForever" (not just
"CodeBrix.Compression"). Always use this full package name when installing. The
assembly and the namespaces are named "CodeBrix.Compression" - the
".MitLicenseForever" suffix belongs to the package id only.

================================================================================

KEY NAMESPACES / USINGS
=======================

    using CodeBrix.Compression.Zip;         // Zip archive operations
    using CodeBrix.Compression.GZip;        // GZip compression/decompression
    using CodeBrix.Compression.Tar;         // Tar archive operations
    using CodeBrix.Compression.BZip2;       // BZip2 compression/decompression
    using CodeBrix.Compression.Dcl;         // PKWARE DCL "implode" decompression
    using CodeBrix.Compression.Checksum;    // Crc32, Adler32
    using CodeBrix.Compression.Core;        // Name transforms, filters, scanning
    using CodeBrix.Compression.Encryption;  // Zip AES transform/stream internals
    using CodeBrix.Compression.Lzw;         // LZW (.Z) decompression stream

The root namespace CodeBrix.Compression itself holds CompressionExceptionBase
(the base class most library exceptions derive from) and the static
CompressionOptions class of global settings; CodeBrix.Compression.Zip.Compression
and CodeBrix.Compression.Zip.Compression.Streams hold the raw Deflate/Inflate
layer that the Zip and GZip streams are built on.

================================================================================

CORE API REFERENCE
==================
The API reference is organised by feature area in the sections that follow.

SUPPORTED FORMATS AND CAPABILITIES
==================================
Format   | Create | Read | Extract | Update | Encrypt
---------|--------|------|---------|--------|--------
Zip      | Yes    | Yes  | Yes     | Yes    | Yes (AES-128, AES-256, ZipCrypto)
GZip     | Yes    | Yes  | Yes     | No     | No
Tar      | Yes    | Yes  | Yes     | No     | No
BZip2    | Yes    | Yes  | Yes     | No     | No
DCL      | No     | Yes  | Yes     | No     | No

Additional features:
  - Zip64 extensions for large files (>4GB)
  - Streaming (non-seekable) input and output
  - In-memory archive operations
  - Checksums: CRC-32, Adler32, BZip2 CRC
  - Unicode filename support
  - Timestamp preservation on extraction
  - Directory structure preservation
  - Path traversal attack prevention

"Update" for Zip means adding, replacing and deleting entries in an EXISTING
archive without rewriting it by hand - see UPDATING AN EXISTING ZIP ARCHIVE
below.

================================================================================

ZIP ARCHIVES
============

--- CREATING A ZIP ARCHIVE ---

Using ZipOutputStream (stream-based, full control):

    using CodeBrix.Compression.Zip;

    using var fileStream = File.Create("archive.zip");
    using var zipStream = new ZipOutputStream(fileStream);

    zipStream.SetLevel(9); // Compression level: 0 (none) to 9 (maximum)

    var entry = new ZipEntry("document.txt")
    {
        DateTime = DateTime.Now
    };

    zipStream.PutNextEntry(entry);

    var buffer = File.ReadAllBytes("document.txt");
    zipStream.Write(buffer, 0, buffer.Length);

    zipStream.CloseEntry();
    zipStream.Finish();

Adding multiple files:

    using CodeBrix.Compression.Zip;

    using var fileStream = File.Create("archive.zip");
    using var zipStream = new ZipOutputStream(fileStream);
    zipStream.SetLevel(9);

    string[] filesToAdd = { "file1.txt", "file2.txt", "file3.dat" };

    foreach (var filePath in filesToAdd)
    {
        var entry = new ZipEntry(Path.GetFileName(filePath))
        {
            DateTime = DateTime.Now
        };

        zipStream.PutNextEntry(entry);

        var buffer = File.ReadAllBytes(filePath);
        zipStream.Write(buffer, 0, buffer.Length);

        zipStream.CloseEntry();
    }

    zipStream.Finish();

--- CREATING AN ENCRYPTED ZIP ARCHIVE ---

AES-256 encryption:

    using CodeBrix.Compression.Zip;

    using var fileStream = File.Create("encrypted.zip");
    using var zipStream = new ZipOutputStream(fileStream);

    zipStream.SetLevel(9);
    zipStream.Password = "my-secret-password";

    var entry = new ZipEntry("confidential.txt")
    {
        AESKeySize = 256,    // Use AES-256 encryption (also supports 128)
        DateTime = DateTime.Now
    };

    zipStream.PutNextEntry(entry);

    var buffer = File.ReadAllBytes("confidential.txt");
    zipStream.Write(buffer, 0, buffer.Length);

    zipStream.CloseEntry();
    zipStream.Finish();

AES-128 encryption:

    var entry = new ZipEntry("file.txt")
    {
        AESKeySize = 128,    // Use AES-128 encryption
        DateTime = DateTime.Now
    };

--- EXTRACTING A ZIP ARCHIVE ---

Using ZipInputStream (stream-based):

    using CodeBrix.Compression.Zip;

    using var fileStream = File.OpenRead("archive.zip");
    using var zipStream = new ZipInputStream(fileStream);

    ZipEntry entry;
    while ((entry = zipStream.GetNextEntry()) != null)
    {
        Console.WriteLine($"Extracting: {entry.Name} ({entry.Size} bytes)");

        using var outputStream = File.Create(entry.Name);
        zipStream.CopyTo(outputStream);
    }

Extracting encrypted archives:

    using var fileStream = File.OpenRead("encrypted.zip");
    using var zipStream = new ZipInputStream(fileStream);
    zipStream.Password = "my-secret-password";

    ZipEntry entry;
    while ((entry = zipStream.GetNextEntry()) != null)
    {
        using var outputStream = File.Create(entry.Name);
        zipStream.CopyTo(outputStream);
    }

--- READING A ZIP ARCHIVE WITH ZipFile ---

ZipFile provides random access to archive entries (requires seekable stream):

    using CodeBrix.Compression.Zip;

    using var zipFile = new ZipFile("archive.zip");

    foreach (ZipEntry entry in zipFile)
    {
        if (!entry.IsFile) continue;

        Console.WriteLine($"{entry.Name} - {entry.Size} bytes");

        using var stream = zipFile.GetInputStream(entry);
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        Console.WriteLine(content);
    }

ZipFile also exposes an integer indexer (zipFile[0]) and Count, and can verify
an archive:

    bool ok = zipFile.TestArchive(testData: true);

    // Or with a strategy and a per-entry result callback:
    bool ok2 = zipFile.TestArchive(
        testData: true,
        strategy: TestStrategy.FindAllErrors,
        resultHandler: (status, message) => Console.WriteLine(message));

TestStrategy values are FindFirstError and FindAllErrors. The result handler is
the ZipTestResultHandler delegate: void (TestStatus status, string message).

--- UPDATING AN EXISTING ZIP ARCHIVE ---

ZipFile is the only type in this package that can UPDATE an existing archive:
add, replace and delete entries in place. All update calls must be bracketed by
BeginUpdate() and CommitUpdate() (or AbortUpdate() to throw the batch away):

    using CodeBrix.Compression.Zip;

    using var zipFile = new ZipFile("archive.zip");

    zipFile.BeginUpdate();
    zipFile.Add("newfile.txt");                 // add from the file system
    zipFile.Add("localname.txt", "in/zip.txt"); // add under a different entry name
    zipFile.AddDirectory("subfolder");          // add a directory entry
    zipFile.Delete("obsolete.txt");             // delete by entry name
    zipFile.CommitUpdate();

Creating a brand-new archive through the same API:

    using var zipFile = ZipFile.Create("new.zip");   // also: ZipFile.Create(Stream)
    zipFile.BeginUpdate();
    zipFile.Add("data.bin");
    zipFile.CommitUpdate();

The update methods on ZipFile:

    void BeginUpdate()
    void BeginUpdate(IArchiveStorage archiveStorage)
    void BeginUpdate(IArchiveStorage archiveStorage, IDynamicDataSource dataSource)
    void CommitUpdate()
    void AbortUpdate()
    void Add(string fileName)
    void Add(string fileName, string entryName)
    void Add(string fileName, CompressionMethod compressionMethod)
    void Add(string fileName, CompressionMethod compressionMethod, bool useUnicodeText)
    void Add(ZipEntry entry)
    void Add(IStaticDataSource dataSource, string entryName)
    void Add(IStaticDataSource dataSource, string entryName, CompressionMethod compressionMethod)
    void Add(IStaticDataSource dataSource, string entryName, CompressionMethod compressionMethod, bool useUnicodeText)
    void Add(IStaticDataSource dataSource, ZipEntry entry)
    void AddDirectory(string directoryName)
    bool Delete(string fileName)      // returns false when no such entry
    void Delete(ZipEntry entry)

Adding content that is not a file on disk: implement IStaticDataSource, whose
single member is "Stream GetSource()", and hand it to one of the Add overloads
above. Ideally GetSource() opens a NEW stream each time it is called, to avoid
locking problems.

Where the update is staged is controlled by IArchiveStorage:

    DiskArchiveStorage(ZipFile file)                          // Safe mode
    DiskArchiveStorage(ZipFile file, FileUpdateMode updateMode)
    MemoryArchiveStorage()                                    // Safe mode
    MemoryArchiveStorage(FileUpdateMode updateMode)

FileUpdateMode values:
  - FileUpdateMode.Safe    Perform all updates on temporary files, so the
                           original archive survives a failure (the default).
  - FileUpdateMode.Direct  Update the archive in place; faster, but a failure
                           can leave the archive damaged.

ZipFile.UpdateMode reports the FileUpdateMode of the storage in use. The
no-argument BeginUpdate() uses disk storage in Safe mode for a file-backed
ZipFile; pass a MemoryArchiveStorage explicitly when the archive lives in a
MemoryStream.

Zip64 behavior during an update is controlled by ZipFile.UseZip64, whose values
are UseZip64.Off, UseZip64.On and UseZip64.Dynamic.

--- FASTZIP (HIGH-LEVEL CONVENIENCE API) ---

FastZip provides simple one-call methods for common zip operations:

Create a zip from an entire directory:

    using CodeBrix.Compression.Zip;

    var fastZip = new FastZip();

    // Create a zip from a directory (recursive)
    fastZip.CreateZip("backup.zip", @"/path/to/folder", recurse: true, fileFilter: null);

    // Create with file filter (e.g., only .txt files)
    fastZip.CreateZip("texts.zip", @"/path/to/folder", recurse: true, fileFilter: @"\.txt$");

Extract a zip to a directory:

    var fastZip = new FastZip();
    fastZip.ExtractZip("backup.zip", @"/path/to/output", fileFilter: null);

FastZip with encryption:

    var fastZip = new FastZip
    {
        Password = "my-password",
        EntryEncryptionMethod = ZipEncryptionMethod.AES256
    };

    fastZip.CreateZip("encrypted-backup.zip", @"/path/to/folder", recurse: true, fileFilter: null);
    fastZip.ExtractZip("encrypted-backup.zip", @"/path/to/output", fileFilter: null);

FastZip options:

    var fastZip = new FastZip
    {
        CreateEmptyDirectories = true,         // Preserve empty directory structure
        RestoreDateTimeOnExtract = true,       // Preserve file timestamps
        Password = "optional-password",
        EntryEncryptionMethod = ZipEncryptionMethod.AES256  // or AES128, ZipCrypto
    };

Available ZipEncryptionMethod values:
  - ZipEncryptionMethod.None        (no encryption; this is the default)
  - ZipEncryptionMethod.ZipCrypto   (legacy and weak; it is marked [Obsolete], so
                                     referencing it produces a compiler warning)
  - ZipEncryptionMethod.AES128      (AES 128-bit)
  - ZipEncryptionMethod.AES256      (AES 256-bit, recommended)

The fileFilter and directoryFilter arguments are regular-expression filters, and
the same filter syntax is available directly through the name/path filter types
in CodeBrix.Compression.Core.

--- ZIP ENTRY PROPERTIES ---

    var entry = new ZipEntry("filename.txt");

    entry.DateTime          // File modification date/time
    entry.Size              // Uncompressed size
    entry.CompressedSize    // Compressed size
    entry.AESKeySize        // AES key size (0, 128, or 256)
    entry.IsFile            // true if entry is a file
    entry.IsDirectory       // true if entry is a directory
    entry.Name              // Entry name/path within archive

--- ZIP COMPRESSION LEVELS ---

    zipStream.SetLevel(0);  // No compression (store only)
    zipStream.SetLevel(1);  // Fastest compression
    zipStream.SetLevel(5);  // Balanced
    zipStream.SetLevel(9);  // Maximum compression (slowest)

--- STREAM OWNERSHIP ---

By default, closing a ZipOutputStream/ZipInputStream closes the underlying
stream. To prevent this:

    zipStream.IsStreamOwner = false;

This is important when working with MemoryStreams or shared streams where
you need to continue using the underlying stream after closing the zip stream.

--- IN-MEMORY ZIP OPERATIONS ---

Create a zip archive entirely in memory:

    using CodeBrix.Compression.Zip;

    using var memoryStream = new MemoryStream();
    using var zipStream = new ZipOutputStream(memoryStream);
    zipStream.IsStreamOwner = false; // Keep MemoryStream open after closing zip

    zipStream.SetLevel(9);

    var entry = new ZipEntry("data.txt") { DateTime = DateTime.Now };
    zipStream.PutNextEntry(entry);

    var data = Encoding.UTF8.GetBytes("Hello, World!");
    zipStream.Write(data, 0, data.Length);

    zipStream.CloseEntry();
    zipStream.Close();

    // memoryStream now contains the complete zip archive
    byte[] zipBytes = memoryStream.ToArray();

================================================================================

GZIP COMPRESSION
================

Simple compress/decompress with static methods:

    using CodeBrix.Compression.GZip;

    // Compress a file
    GZip.Compress(
        File.OpenRead("data.txt"),
        File.Create("data.txt.gz"),
        isStreamOwner: true);

    // Decompress a file
    GZip.Decompress(
        File.OpenRead("data.txt.gz"),
        File.Create("data.txt"),
        isStreamOwner: true);

GZip.Compress also accepts two OPTIONAL arguments after isStreamOwner - a copy
buffer size and a Deflate compression level:

    GZip.Compress(input, output, isStreamOwner: true, bufferSize: 512, level: 6);

The defaults are bufferSize 512 and level 6; pass level 9 for maximum
compression or 1 for the fastest. GZip.Decompress has no such parameters.

Using GZipOutputStream for more control:

    using CodeBrix.Compression.GZip;

    using var fileStream = File.Create("data.gz");
    using var gzipStream = new GZipOutputStream(fileStream);

    var buffer = File.ReadAllBytes("data.txt");
    gzipStream.Write(buffer, 0, buffer.Length);

Using GZipInputStream for decompression:

    using CodeBrix.Compression.GZip;

    using var fileStream = File.OpenRead("data.gz");
    using var gzipStream = new GZipInputStream(fileStream);

    using var outputStream = File.Create("data.txt");
    gzipStream.CopyTo(outputStream);

GZip stream ownership:

    var gzipStream = new GZipOutputStream(underlyingStream);
    gzipStream.IsStreamOwner = false; // Don't close underlying stream

IMPORTANT: the isStreamOwner parameter controls whether the input/output streams
are automatically closed when the GZip operation completes. Set to true when
you want automatic cleanup, false when you need to continue using the streams.

================================================================================

TAR ARCHIVES
============

--- CREATING A TAR ARCHIVE ---

Using TarArchive (high-level):

    using CodeBrix.Compression.Tar;

    using var outStream = File.Create("archive.tar");
    using var tarArchive = TarArchive.CreateOutputTarArchive(outStream);

    var tarEntry = TarEntry.CreateEntryFromFile("document.txt");
    tarArchive.WriteEntry(tarEntry, recurse: false);

    tarArchive.Close();

Creating from a directory (recursive):

    using CodeBrix.Compression.Tar;

    using var outStream = File.Create("archive.tar");
    using var tarArchive = TarArchive.CreateOutputTarArchive(outStream);

    tarArchive.RootPath = "/path/to/source/folder";

    var entry = TarEntry.CreateEntryFromFile("/path/to/source/folder");
    tarArchive.WriteEntry(entry, recurse: true);

Using TarOutputStream (low-level, full control):

    using CodeBrix.Compression.Tar;

    using var outStream = File.Create("archive.tar");
    using var tarOut = new TarOutputStream(outStream, nameEncoding: null);

    var entry = TarEntry.CreateTarEntry("myfile.txt");
    entry.Size = fileData.Length; // MUST set size before writing
    entry.ModTime = DateTime.Now;

    tarOut.PutNextEntry(entry);
    tarOut.Write(fileData, 0, fileData.Length);
    tarOut.CloseEntry();

--- EXTRACTING A TAR ARCHIVE ---

Using TarArchive (high-level):

    using CodeBrix.Compression.Tar;

    using var inStream = File.OpenRead("archive.tar");
    using var tarArchive = TarArchive.CreateInputTarArchive(inStream, nameEncoding: null);

    tarArchive.ExtractContents("/path/to/output");

Using TarInputStream (low-level):

    using CodeBrix.Compression.Tar;

    using var inStream = File.OpenRead("archive.tar");
    using var tarIn = new TarInputStream(inStream, nameEncoding: null);

    TarEntry entry;
    while ((entry = tarIn.GetNextEntry()) != null)
    {
        Console.WriteLine($"{entry.Name} - {entry.Size} bytes");

        // Read entry data...
        using var outputStream = File.Create(entry.Name);
        tarIn.CopyEntryContents(outputStream);
    }

--- TAR WITH GZIP (tar.gz) ---

Create a tar.gz archive:

    using CodeBrix.Compression.GZip;
    using CodeBrix.Compression.Tar;

    using var fileStream = File.Create("archive.tar.gz");
    using var gzipStream = new GZipOutputStream(fileStream);
    using var tarArchive = TarArchive.CreateOutputTarArchive(gzipStream);

    tarArchive.IsStreamOwner = false;

    var entry = TarEntry.CreateEntryFromFile("document.txt");
    tarArchive.WriteEntry(entry, recurse: false);

Extract a tar.gz archive:

    using CodeBrix.Compression.GZip;
    using CodeBrix.Compression.Tar;

    using var fileStream = File.OpenRead("archive.tar.gz");
    using var gzipStream = new GZipInputStream(fileStream);
    using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, nameEncoding: null);

    tarArchive.ExtractContents("/path/to/output");

--- TAR ENTRY PROPERTIES ---

    var entry = TarEntry.CreateTarEntry("name");

    entry.Name          // Entry name/path
    entry.Size          // File size (MUST be set before writing data)
    entry.ModTime       // Modification time (seconds precision only)
    entry.UserId        // User ID
    entry.GroupId       // Group ID
    entry.UserName      // User name
    entry.GroupName     // Group name (defaults to "None" if set to null)
    entry.IsDirectory   // true if entry is a directory
    entry.File          // Associated file path (if created from file)

    entry.TarHeader.Mode       // File permissions (e.g., 33188 for 644)
    entry.TarHeader.LinkName   // Symbolic link target
    entry.TarHeader.Magic      // TAR magic string
    entry.TarHeader.Version    // TAR version
    entry.TarHeader.DevMajor   // Device major number
    entry.TarHeader.DevMinor   // Device minor number
    entry.TarHeader.Checksum   // Header checksum
    entry.TarHeader.IsChecksumValid  // Validate checksum

--- TAR ENCODING SUPPORT ---

Tar archives support different character encodings for filenames:

    using System.Text;

    // Register encoding provider for non-UTF8 encodings
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    // Create with specific encoding
    using var tarOut = new TarOutputStream(stream, Encoding.UTF8);

    // Read with specific encoding
    using var tarIn = new TarInputStream(stream, Encoding.GetEncoding("shift-jis"));

    // Use null for default encoding behavior
    using var tarOut = new TarOutputStream(stream, nameEncoding: null);

--- TAR ASYNC SUPPORT ---

    var entry = await tarInputStream.GetNextEntryAsync(CancellationToken.None);
    var bytesRead = await tarInputStream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

--- TAR BLOCK FACTOR ---

The block factor controls the record size (default is typically 20):

    // Create with custom block factor (1-64)
    using var tarOut = new TarOutputStream(stream, blockFactor: 10, nameEncoding: null);

--- TAR STREAM OWNERSHIP ---

    tarArchive.IsStreamOwner = false;  // Don't close underlying stream
    tarOutputStream.IsStreamOwner = false;
    tarInputStream.IsStreamOwner = false;

--- TAR IMPORTANT NOTES ---

1. ModTime only stores seconds precision (no milliseconds/ticks)
2. Size MUST be set on TarEntry BEFORE writing data to TarOutputStream
3. Long filenames are handled automatically (extended headers)
4. Setting entry.Name to null throws ArgumentNullException
5. Setting entry.Size to negative throws ArgumentOutOfRangeException
6. Setting entry.ModTime to DateTime.MinValue throws ArgumentOutOfRangeException
7. TarEntry supports Clone() for creating deep copies
8. RootPath property on TarArchive controls the base path for entries

================================================================================

BZIP2 COMPRESSION
=================

Simple compress/decompress with static methods:

    using CodeBrix.Compression.BZip2;

    // Compress with level (1-9, where 9 is maximum compression)
    BZip2.Compress(
        File.OpenRead("data.txt"),
        File.Create("data.txt.bz2"),
        isStreamOwner: true,
        level: 9);

    // Decompress
    BZip2.Decompress(
        File.OpenRead("data.txt.bz2"),
        File.Create("data.txt"),
        isStreamOwner: true);

Note that the level argument of BZip2.Compress is REQUIRED (unlike GZip.Compress,
where it is optional).

Using BZip2OutputStream / BZip2InputStream for more control:

    using CodeBrix.Compression.BZip2;

    // Compress
    using var fileStream = File.Create("data.bz2");
    using var bz2Stream = new BZip2OutputStream(fileStream);
    var buffer = File.ReadAllBytes("data.txt");
    bz2Stream.Write(buffer, 0, buffer.Length);

    // Decompress
    using var inputStream = File.OpenRead("data.bz2");
    using var bz2Input = new BZip2InputStream(inputStream);
    using var outputStream = File.Create("data.txt");
    bz2Input.CopyTo(outputStream);

================================================================================

DCL DECOMPRESSION (PKWARE DATA COMPRESSION LIBRARY "IMPLODE" FORMAT)
====================================================================

DCL is the stream format produced by the implode() function of the PKWARE
Data Compression Library (1990-92). It was licensed to many MS-DOS-era
products and is common in installer archives and self-extracting shareware
distributions of that period (it is also the compression used inside MPQ
game archives).

IMPORTANT: DCL "implode" is NOT the same as the Zip archive "Imploded"
compression method (method 6), and it is not produced by PKZIP itself.
A DCL stream is a raw compressed stream with no container/archive structure.

A DCL stream starts with two header bytes:
  byte 0: 0 = literals are uncoded, 1 = literals are Huffman coded
  byte 1: 4, 5, or 6 = log2(dictionary size) - 6 (1024, 2048, or 4096 bytes)

Decompression is supported; compression is NOT.

Using DclInputStream (streaming):

    using CodeBrix.Compression.Dcl;

    using var inStream = new DclInputStream(File.OpenRead("data.imploded"));
    using var outStream = File.Create("data.bin");
    inStream.CopyTo(outStream);

Using the static helper:

    using CodeBrix.Compression.Dcl;

    Dcl.Decompress(
        File.OpenRead("data.imploded"),
        File.Create("data.bin"),
        isStreamOwner: true);

NOTE: when calling the static Dcl class from code whose namespace is itself
under a "...Dcl" namespace segment, the bare name "Dcl" may resolve to the
namespace instead of the class; use an alias in that case:

    using DclHelper = CodeBrix.Compression.Dcl.Dcl;

Error handling: malformed or truncated input throws DclException (derived
from CompressionExceptionBase). There is no checksum in the DCL format
itself, so callers wanting integrity verification should compare the output
against an externally known length/checksum.

DclInputStream is read-only, forward-only: CanSeek is false, and Length,
Seek, SetLength, and Write all throw NotSupportedException. IsStreamOwner
(default true) controls whether disposing the DclInputStream also disposes
the underlying stream.

The decoder is a C# port of Mark Adler's "blast" reference decoder (zlib
contrib/blast, version 1.3, zlib license) - see THIRD-PARTY-NOTICES.txt in
the package.

================================================================================

CHECKSUMS
=========

CRC-32:

    using CodeBrix.Compression.Checksum;

    var crc = new Crc32();
    crc.Update(buffer);
    long checksum = crc.Value;

Adler32:

    using CodeBrix.Compression.Checksum;

    var adler = new Adler32();
    adler.Update(buffer);
    long checksum = adler.Value;

================================================================================

ENCRYPTION DETAILS
==================

Supported encryption methods for Zip archives:

1. AES-256 (recommended):
   - Set entry.AESKeySize = 256
   - Requires salt of length 16 bytes (handled internally)
   - Most secure option

2. AES-128:
   - Set entry.AESKeySize = 128
   - Requires salt of length 8 bytes (handled internally)
   - Good balance of security and performance

3. ZipCrypto (legacy):
   - Traditional PKZIP encryption
   - Less secure than AES, use only for compatibility
   - Used when Password is set but AESKeySize is not specified

Key classes (namespace CodeBrix.Compression.Encryption):
  - ZipAESTransform: Handles AES encryption/decryption transforms
    - Valid block sizes: 16 (AES-128) and 32 (AES-256)
    - PwdVerifier property returns a 2-byte verification array
  - ZipAESStream: AES encryption stream (read-mode only)

IMPORTANT: ZipAESStream only supports CryptoStreamMode.Read.
Attempting to construct it in Write mode will throw an exception.

Most consuming code never touches these two types directly: set Password on the
ZipOutputStream/ZipInputStream/ZipFile (or on FastZip, together with
EntryEncryptionMethod) and the library selects the transform for you.

================================================================================

COMPLETE EXAMPLES
=================

Example 1: Create a Password-Protected Zip with Multiple Files
----------------------------------------------------------------
    using CodeBrix.Compression.Zip;

    using var fileStream = File.Create("secure-archive.zip");
    using var zipStream = new ZipOutputStream(fileStream);

    zipStream.SetLevel(9);
    zipStream.Password = "strong-password-123";

    string[] files = { "report.pdf", "data.csv", "config.json" };

    foreach (var filePath in files)
    {
        var entry = new ZipEntry(Path.GetFileName(filePath))
        {
            AESKeySize = 256,
            DateTime = File.GetLastWriteTime(filePath)
        };

        zipStream.PutNextEntry(entry);

        using var inputStream = File.OpenRead(filePath);
        inputStream.CopyTo(zipStream);

        zipStream.CloseEntry();
    }

    zipStream.Finish();

Example 2: Extract All Files from a Zip Preserving Directory Structure
-----------------------------------------------------------------------
    using CodeBrix.Compression.Zip;

    var fastZip = new FastZip
    {
        CreateEmptyDirectories = true,
        RestoreDateTimeOnExtract = true
    };

    fastZip.ExtractZip("archive.zip", "/output/directory", fileFilter: null);

Example 3: Create a tar.gz Backup of a Directory
--------------------------------------------------
    using CodeBrix.Compression.GZip;
    using CodeBrix.Compression.Tar;

    using var fileStream = File.Create("backup.tar.gz");
    using var gzipStream = new GZipOutputStream(fileStream);
    using var tarArchive = TarArchive.CreateOutputTarArchive(gzipStream);

    tarArchive.RootPath = "/path/to/backup/source";

    var entry = TarEntry.CreateEntryFromFile("/path/to/backup/source");
    tarArchive.WriteEntry(entry, recurse: true);

Example 4: In-Memory Zip Creation and Extraction
--------------------------------------------------
    using CodeBrix.Compression.Zip;

    // Create in memory
    var memStream = new MemoryStream();
    using (var zipOut = new ZipOutputStream(memStream))
    {
        zipOut.IsStreamOwner = false;
        zipOut.SetLevel(5);

        var entry = new ZipEntry("hello.txt") { DateTime = DateTime.Now };
        zipOut.PutNextEntry(entry);

        var bytes = Encoding.UTF8.GetBytes("Hello from memory!");
        zipOut.Write(bytes, 0, bytes.Length);
        zipOut.CloseEntry();
        zipOut.Finish();
    }

    // Read from memory
    memStream.Position = 0;
    using var zipFile = new ZipFile(memStream);

    foreach (ZipEntry entry in zipFile)
    {
        if (!entry.IsFile) continue;

        using var stream = zipFile.GetInputStream(entry);
        using var reader = new StreamReader(stream);
        Console.WriteLine(reader.ReadToEnd());
    }

Example 5: Compress Data with BZip2 In-Memory
-----------------------------------------------
    using CodeBrix.Compression.BZip2;

    byte[] originalData = Encoding.UTF8.GetBytes("Data to compress...");

    // Compress
    using var compressedStream = new MemoryStream();
    using (var bz2Out = new BZip2OutputStream(compressedStream))
    {
        bz2Out.IsStreamOwner = false;
        bz2Out.Write(originalData, 0, originalData.Length);
    }

    byte[] compressed = compressedStream.ToArray();

    // Decompress
    using var inputStream = new MemoryStream(compressed);
    using var bz2In = new BZip2InputStream(inputStream);
    using var resultStream = new MemoryStream();
    bz2In.CopyTo(resultStream);

    byte[] decompressed = resultStream.ToArray();

Example 6: Read Zip Contents Without Extracting
-------------------------------------------------
    using CodeBrix.Compression.Zip;

    using var zipFile = new ZipFile("archive.zip");

    Console.WriteLine($"Archive contains {zipFile.Count} entries:");

    foreach (ZipEntry entry in zipFile)
    {
        var type = entry.IsDirectory ? "DIR " : "FILE";
        Console.WriteLine($"  [{type}] {entry.Name} ({entry.Size} bytes, " +
            $"compressed: {entry.CompressedSize} bytes)");
    }

Example 7: Replace One Entry Inside an Existing Zip
-----------------------------------------------------
    using CodeBrix.Compression.Zip;

    using var zipFile = new ZipFile("archive.zip");

    zipFile.BeginUpdate();
    zipFile.Delete("config.json");            // no-op returning false if absent
    zipFile.Add("new-config.json", "config.json");
    zipFile.CommitUpdate();

    if (!zipFile.TestArchive(testData: true))
    {
        throw new InvalidOperationException("Updated archive failed verification.");
    }

================================================================================

MINIMUM VIABLE PROJECT
======================

To scaffold a new .NET 10 console project that uses CodeBrix.Compression:

    dotnet new console -n MyCompressionApp --framework net10.0
    cd MyCompressionApp
    dotnet add package CodeBrix.Compression.MitLicenseForever

Then in Program.cs:

    using CodeBrix.Compression.Zip;

    // Create a simple zip archive
    using var fileStream = File.Create("output.zip");
    using var zipStream = new ZipOutputStream(fileStream);
    zipStream.SetLevel(9);

    var entry = new ZipEntry("hello.txt") { DateTime = DateTime.Now };
    zipStream.PutNextEntry(entry);

    var data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
    zipStream.Write(data, 0, data.Length);

    zipStream.CloseEntry();
    zipStream.Finish();

    Console.WriteLine("Created output.zip!");

Build and run:

    dotnet build
    dotnet run

================================================================================

PERFORMANCE TIPS
================

1. USE FASTZIP FOR SIMPLE OPERATIONS: When you just need to zip/unzip a
   directory, FastZip is simpler and handles all the details for you.
   Use ZipOutputStream/ZipInputStream only when you need fine-grained control.

2. SET APPROPRIATE COMPRESSION LEVEL: Level 9 gives maximum compression but
   is slower. Level 5-6 provides a good balance. Level 0 stores without
   compression (fastest, useful for already-compressed content like JPEGs).

3. STREAM OWNERSHIP: Always be explicit about IsStreamOwner when working with
   MemoryStreams or shared streams. Forgetting this is a common source of bugs
   where the underlying stream gets unexpectedly closed.

4. SET SIZE BEFORE WRITING TAR ENTRIES: When using TarOutputStream directly,
   you MUST set entry.Size before calling PutNextEntry and writing data.
   Failure to do this will produce corrupt archives.

5. CALL CloseEntry() AFTER WRITING: For both ZipOutputStream and
   TarOutputStream, always call CloseEntry() after writing each entry's data.

6. CALL Finish() ON ZIP STREAMS: Always call Finish() on ZipOutputStream
   before closing to ensure the central directory is written correctly.

7. USE CopyTo() FOR EXTRACTION: When extracting, use stream.CopyTo() rather
   than manual buffer reading for cleaner and often faster code.

8. PREFER AES-256 FOR ENCRYPTION: When encryption is needed, use AES-256
   (AESKeySize = 256). Avoid ZipCrypto for new archives as it's less secure.

9. REGISTER ENCODING PROVIDERS: If working with non-ASCII filenames in Tar
   archives, call Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
   before creating/reading archives.

10. USE ZipFile FOR RANDOM ACCESS: When you need to read specific entries
    from a zip without processing all entries sequentially, use ZipFile
    instead of ZipInputStream. ZipFile provides random access via its indexer.

11. BATCH ZIP UPDATES: Do all adds and deletes between ONE BeginUpdate() and
    ONE CommitUpdate(). Committing after every entry rewrites the archive each
    time. FileUpdateMode.Direct avoids the temporary-file copy but risks the
    archive on failure - keep the default Safe mode unless you have measured a
    problem.

================================================================================

COMMON PITFALLS TO AVOID
========================

1. DO NOT confuse the NuGet package name with the namespace.
   - Package: CodeBrix.Compression.MitLicenseForever
   - Namespaces: CodeBrix.Compression.Zip, CodeBrix.Compression.GZip, etc.

2. DO NOT use the upstream SharpZipLib namespaces (ICSharpCode.*). Even though
   this is a fork, all namespaces are CodeBrix.Compression.*, and the two
   libraries must not be mixed in one project.

3. DO NOT forget to call CloseEntry() after writing each zip/tar entry.

4. DO NOT forget to call Finish() on ZipOutputStream before closing.

5. DO NOT target .NET versions below 10. This library requires .NET 10 or later.

6. DO NOT forget to set entry.Size on TarEntry before writing to TarOutputStream.

7. DO NOT attempt to use ZipAESStream in Write mode - it only supports Read.

8. DO NOT forget that Tar ModTime has only seconds precision - do not expect
   millisecond or tick-level timestamp accuracy.

9. DO NOT forget to set IsStreamOwner = false when you need the underlying
   stream to remain open after closing the compression stream.

10. DO NOT assume ZipInputStream can handle all zip files - some features
    (like random access to entries) require ZipFile with a seekable stream.

11. DO NOT call ZipFile.Add/Delete/AddDirectory outside a BeginUpdate() /
    CommitUpdate() pair, and do not forget CommitUpdate() - without it the
    changes are discarded.

12. DO NOT expect ZipFile.Delete(string) to throw when the entry is missing:
    the string overload returns false instead. The ZipEntry overload returns
    void.

13. DO NOT pass a level argument positionally to GZip.Compress thinking it is
    the third parameter - the third parameter is isStreamOwner, and level is
    the FIFTH (after bufferSize). Use named arguments.

14. DO NOT assume the extracted entry name is safe to pass straight to
    File.Create. Entry names come from the archive; join them onto your output
    directory and verify the result stays inside it before writing.

================================================================================

WHAT THIS PACKAGE DOES NOT DO
=============================

Do NOT attempt to use CodeBrix.Compression for the following - it will not work:

  - Extracting Zip entries compressed with the legacy Zip "Implode" (method 6)
    or "Shrink" (method 1) methods (note: the separate PKWARE DCL "implode"
    raw stream format IS supported - see DCL DECOMPRESSION above)
  - Compressing (as opposed to decompressing) DCL "imploded" data
  - Updating GZip, Tar or BZip2 archives in place - only Zip supports update
  - RAR archive creation or extraction
  - 7z (7-Zip) archive creation or extraction
  - XZ compression
  - Zstandard (zstd) compression
  - LZ4 compression
  - Snappy compression
  - Brotli compression (use System.IO.Compression.BrotliStream)
  - Image compression (use CodeBrix.Imaging for image format conversion)
  - PDF creation (use CodeBrix.PdfDocuments instead)
  - File encryption outside of zip archives (AES encryption is zip-specific)
  - Disk image creation (ISO, VHD, etc.)
  - Self-extracting archive creation

This library IS for: creating, reading, extracting, and updating archives in
Zip, GZip, Tar, and BZip2 formats, with optional AES encryption for Zip; and
for decompressing raw PKWARE DCL "imploded" streams.

================================================================================

WORKING EXAMPLES ON GITHUB
==========================

The CodeBrix.Compression.Tests project in the source repository contains
extensive working code examples. If the documentation above is not sufficient
for a specific task, fetch and read the relevant test file:

    https://github.com/ellisnet/CodeBrix.Compression/tree/main/tests/CodeBrix.Compression.Tests

Feature-to-test-file mapping:

  FastZip (high-level zip/unzip, encryption methods, unicode filenames,
  timestamp preservation, directory handling, stream ownership):
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/FastZipHandling.cs

  General zip archive handling:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/GeneralHandling.cs

  Zip stream operations (streaming input/output):
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/StreamHandling.cs

  Zip async operations:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipStreamAsyncTests.cs

  ZipFile random access, BeginUpdate/Add/Delete/CommitUpdate, TestArchive:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipFileHandling.cs

  Zip entry creation and properties:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipEntryHandling.cs

  Zip entry factory patterns:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipEntryFactoryHandling.cs

  Zip encryption (AES-128, AES-256, ZipCrypto):
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipEncryptionHandling.cs

  AES encryption internals (transforms, streams, salt/block validation):
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Encryption/EncryptionTests.cs

  Zip extra data fields:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipExtraDataHandling.cs

  Zip name transforms and path handling:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipNameTransformHandling.cs
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/WindowsNameTransformHandling.cs

  Zip string encoding:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipStringsTests.cs

  Zip corruption handling:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipCorruptionHandling.cs

  Zip passthrough operations:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/PassthroughTests.cs

  Core zip test infrastructure (in-memory creation, data verification):
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Zip/ZipTests.cs

  GZip compression/decompression, stream ownership, flushing, error handling:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/GZip/GZipTests.cs
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/GZip/GZipAsyncTests.cs

  Tar archives (create, read, extract, long names, encoding, async,
  entry properties, checksums, stream ownership, tar.gz integration):
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Tar/TarTests.cs
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Tar/TarArchiveTests.cs
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Tar/TarInputStreamTests.cs
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Tar/TarBufferTests.cs

  BZip2 compression/decompression:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/BZip2/Bzip2Tests.cs

  Raw Deflate/Inflate layer:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Base/InflaterDeflaterTests.cs

  Checksum operations (CRC-32, Adler32):
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Checksum/ChecksumTests.cs

  LZW (.Z) decompression:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Lzw/LzwTests.cs

  DCL (PKWARE DCL "implode") decompression:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Dcl/DclTests.cs

  Core helpers (name and path filters, file-system scanning, name transforms):
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Core/CoreTests.cs

  Exception serialization:
    -> https://github.com/ellisnet/CodeBrix.Compression/blob/main/tests/CodeBrix.Compression.Tests/Serialization/SerializationTests.cs

HOW TO USE: to fetch the raw text of any of the files above, replace
"https://github.com/ellisnet/CodeBrix.Compression/blob/main/" with
"https://raw.githubusercontent.com/ellisnet/CodeBrix.Compression/main/".

================================================================================

QUICK REFERENCE CARD
====================

--- ZIP ---
ZipOutputStream(stream)           Create zip output stream
  .SetLevel(0-9)                  Set compression level
  .Password = "..."               Set password for encryption
  .PutNextEntry(entry)            Start writing an entry
  .CloseEntry()                   Finish writing current entry
  .Finish()                       Finalize the archive
  .IsStreamOwner                  Control underlying stream disposal

ZipInputStream(stream)            Create zip input stream
  .GetNextEntry()                 Get next entry (null when done)
  .Password = "..."               Set password for decryption

ZipFile("path")                   Open zip for random access
ZipFile(stream)                   Open zip from stream
ZipFile.Create("path")            Create a new, empty zip for update
ZipFile.Create(stream)            Create a new, empty zip in a stream
  .GetInputStream(entry)          Get stream for specific entry
  .Count                          Number of entries
  [index]                         Entry by position
  .Password                       Password for encrypted entries
  .UseZip64                       Off | On | Dynamic
  .UpdateMode                     FileUpdateMode of the active storage
  .TestArchive(testData)          Verify the archive
  .TestArchive(testData, strategy, resultHandler)

  Zip UPDATE API (all between BeginUpdate and CommitUpdate):
  .BeginUpdate()                  Start a batch of changes
  .BeginUpdate(archiveStorage)    Start a batch using explicit storage
  .BeginUpdate(archiveStorage, dataSource)
  .Add(fileName)                  Add a file from disk
  .Add(fileName, entryName)       Add a file under a different entry name
  .Add(fileName, compressionMethod[, useUnicodeText])
  .Add(entry)                     Add an empty/prepared ZipEntry
  .Add(dataSource, entryName[, compressionMethod[, useUnicodeText]])
  .Add(dataSource, entry)         Add from an IStaticDataSource
  .AddDirectory(directoryName)    Add a directory entry
  .Delete(fileName)               Delete by name; returns false if absent
  .Delete(entry)                  Delete a specific ZipEntry
  .CommitUpdate()                 Apply the batch
  .AbortUpdate()                  Discard the batch

IStaticDataSource                 Stream GetSource()
IArchiveStorage                   Update staging abstraction
DiskArchiveStorage(file[, updateMode])
MemoryArchiveStorage([updateMode])
FileUpdateMode                    Safe | Direct
TestStrategy                      FindFirstError | FindAllErrors
ZipTestResultHandler              void (TestStatus status, string message)

ZipEntry("name")                  Create a zip entry
  .DateTime                       Modification date
  .AESKeySize                     0, 128, or 256
  .Size / .CompressedSize         File sizes
  .IsFile / .IsDirectory          Entry type

FastZip                           High-level convenience class
  .CreateZip(zip, dir, recurse, filter)
  .ExtractZip(zip, dir, filter)
  .Password                       Encryption password
  .EntryEncryptionMethod          None (default), ZipCrypto, AES128, AES256
  .CreateEmptyDirectories         Preserve empty dirs
  .RestoreDateTimeOnExtract       Preserve timestamps

--- GZIP ---
GZip.Compress(inStream, outStream, isStreamOwner, bufferSize = 512, level = 6)
GZip.Decompress(inStream, outStream, isStreamOwner)
GZipOutputStream(stream)          Compression stream
GZipInputStream(stream)           Decompression stream

--- TAR ---
TarArchive.CreateOutputTarArchive(stream)
TarArchive.CreateInputTarArchive(stream, nameEncoding)
  .WriteEntry(entry, recurse)
  .ExtractContents(path)
  .RootPath                       Base path for entries
  .IsStreamOwner

TarOutputStream(stream, nameEncoding)
TarOutputStream(stream, blockFactor, nameEncoding)
  .PutNextEntry(entry)
  .CloseEntry()

TarInputStream(stream, nameEncoding)
  .GetNextEntry()
  .GetNextEntryAsync(ct)

TarEntry.CreateTarEntry("name")    Create entry by name
TarEntry.CreateEntryFromFile(path) Create from file system

--- BZIP2 ---
BZip2.Compress(inStream, outStream, isStreamOwner, level)   // level required
BZip2.Decompress(inStream, outStream, isStreamOwner)
BZip2OutputStream(stream)
BZip2InputStream(stream)

--- DCL ---
Dcl.Decompress(inStream, outStream, isStreamOwner)
DclInputStream(stream)            DCL "implode" decompression stream
  .IsStreamOwner                  Control underlying stream disposal

--- CHECKSUMS ---
Crc32                             CRC-32 checksum
Adler32                           Adler-32 checksum
  .Update(buffer)                 Add data
  .Value                          Get checksum value

================================================================================
