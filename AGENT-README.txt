================================================================================
AGENT-README: CodeBrix.Compression
A Comprehensive Guide for AI Coding Agents
================================================================================

OVERVIEW
--------
CodeBrix.Compression is a .NET library for creating, reading, updating, and
extracting compressed archives in multiple formats: Zip, GZip, Tar, and BZip2.

It supports encryption (AES-128, AES-256, ZipCrypto), Zip64 extensions for large
files, streaming operations, in-memory archive operations, and checksums.

CodeBrix.Compression has ZERO external dependencies beyond .NET itself.

It is a fork of the popular SharpZipLib library version 1.4.2. If you are
familiar with SharpZipLib, the API surface is very similar, but ALL namespaces
use "CodeBrix.Compression" instead of "ICSharpCode.SharpZipLib".
Do NOT mix the two libraries.

Source Repository: https://github.com/ellisnet/CodeBrix.Compression
License: MIT License

================================================================================

INSTALLATION
------------
NuGet Package: CodeBrix.Compression.MitLicenseForever
Latest Version: 1.0.49 (as of Feb 2026)
Package Size: ~131 KB
Dependencies: None

Requirements: .NET 10.0 or higher

To add to a .NET 10+ project:

    dotnet add package CodeBrix.Compression.MitLicenseForever

Or in a .csproj file:

    <PackageReference Include="CodeBrix.Compression.MitLicenseForever" Version="1.0.49" />

IMPORTANT: The package name is "CodeBrix.Compression.MitLicenseForever" (not just
"CodeBrix.Compression"). Always use this full package name when installing.

================================================================================

KEY NAMESPACES
--------------

    using CodeBrix.Compression.Zip;    // Zip archive operations
    using CodeBrix.Compression.GZip;   // GZip compression/decompression
    using CodeBrix.Compression.Tar;    // Tar archive operations
    using CodeBrix.Compression.BZip2;  // BZip2 compression/decompression

================================================================================

SUPPORTED FORMATS AND CAPABILITIES
------------------------------------
Format   | Create | Read | Extract | Update | Encrypt
---------|--------|------|---------|--------|--------
Zip      | Yes    | Yes  | Yes     | Yes    | Yes (AES-128, AES-256, ZipCrypto)
GZip     | Yes    | Yes  | Yes     | No     | No
Tar      | Yes    | Yes  | Yes     | No     | No
BZip2    | Yes    | Yes  | Yes     | No     | No

Additional features:
  - Zip64 extensions for large files (>4GB)
  - Streaming (non-seekable) input and output
  - In-memory archive operations
  - Checksums: CRC-32, Adler32, BZip2 CRC
  - Unicode filename support
  - Timestamp preservation on extraction
  - Directory structure preservation
  - Path traversal attack prevention

================================================================================

ZIP ARCHIVES
=============

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
  - ZipEncryptionMethod.ZipCrypto   (legacy, less secure)
  - ZipEncryptionMethod.AES128      (AES 128-bit)
  - ZipEncryptionMethod.AES256      (AES 256-bit, recommended)

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
=================

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

GZip with delayed header writing (useful for HTTP/IIS scenarios):

    // Headers can be delayed for compatibility with certain streaming scenarios
    // The library handles this transparently

IMPORTANT: isStreamOwner parameter controls whether the input/output streams
are automatically closed when the GZip operation completes. Set to true when
you want automatic cleanup, false when you need to continue using the streams.

================================================================================

TAR ARCHIVES
=============

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
==================

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

Key classes:
  - ZipAESTransform: Handles AES encryption/decryption transforms
    - Valid block sizes: 16 (AES-128) and 32 (AES-256)
    - PwdVerifier property returns a 2-byte verification array
  - ZipAESStream: AES encryption stream (read-mode only)

IMPORTANT: ZipAESStream only supports CryptoStreamMode.Read.
Attempting to construct it in Write mode will throw an exception.

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

================================================================================

PERFORMANCE TIPS FOR CODING AGENTS
====================================

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

================================================================================

COMMON PITFALLS TO AVOID
=========================

1. DO NOT confuse the NuGet package name with the namespace.
   - Package: CodeBrix.Compression.MitLicenseForever
   - Namespaces: CodeBrix.Compression.Zip, CodeBrix.Compression.GZip, etc.

2. DO NOT use ICSharpCode.SharpZipLib namespaces. Even though this is a fork,
   all namespaces are CodeBrix.Compression.*.

3. DO NOT forget to call CloseEntry() after writing each zip/tar entry.

4. DO NOT forget to call Finish() on ZipOutputStream before closing.

5. DO NOT target .NET versions below 10.0. This library requires .NET 10+.

6. DO NOT forget to set entry.Size on TarEntry before writing to TarOutputStream.

7. DO NOT attempt to use ZipAESStream in Write mode - it only supports Read.

8. DO NOT forget that Tar ModTime has only seconds precision - do not expect
   millisecond or tick-level timestamp accuracy.

9. DO NOT forget to set IsStreamOwner = false when you need the underlying
   stream to remain open after closing the compression stream.

10. DO NOT assume ZipInputStream can handle all zip files - some features
    (like random access to entries) require ZipFile with a seekable stream.

================================================================================

WHAT THIS LIBRARY DOES NOT DO
===============================

Do NOT attempt to use CodeBrix.Compression for the following - it will not work:

  - RAR archive creation or extraction
  - 7z (7-Zip) archive creation or extraction
  - XZ compression
  - Zstandard (zstd) compression
  - LZ4 compression
  - Snappy compression
  - Image compression (use CodeBrix.Imaging for image format conversion)
  - PDF creation (use CodeBrix.PdfDocuments instead)
  - File encryption outside of zip archives (AES encryption is zip-specific)
  - Disk image creation (ISO, VHD, etc.)
  - Self-extracting archive creation

This library IS for: creating, reading, extracting, and updating archives in
Zip, GZip, Tar, and BZip2 formats, with optional AES encryption for Zip.

================================================================================

MINIMUM VIABLE PROJECT TEMPLATE
=================================

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

DEEPER LEARNING: TEST FILE CROSS-REFERENCES
=============================================

The CodeBrix.Compression.Tests project in the source repository contains
extensive working code examples. If the documentation above is not sufficient
for a specific task, fetch and read the relevant test file from:

    https://github.com/ellisnet/CodeBrix.Compression
    Path: test/CodeBrix.Compression.Tests/

Feature-to-test-file mapping:

  FastZip (high-level zip/unzip, encryption methods, unicode filenames,
  timestamp preservation, directory handling, stream ownership):
    -> test/CodeBrix.Compression.Tests/Zip/FastZipHandling.cs

  General zip archive handling:
    -> test/CodeBrix.Compression.Tests/Zip/GeneralHandling.cs

  Zip stream operations (streaming input/output):
    -> test/CodeBrix.Compression.Tests/Zip/StreamHandling.cs

  Zip async operations:
    -> test/CodeBrix.Compression.Tests/Zip/ZipStreamAsyncTests.cs

  ZipFile random access operations:
    -> test/CodeBrix.Compression.Tests/Zip/ZipFileHandling.cs

  Zip entry creation and properties:
    -> test/CodeBrix.Compression.Tests/Zip/ZipEntryHandling.cs

  Zip entry factory patterns:
    -> test/CodeBrix.Compression.Tests/Zip/ZipEntryFactoryHandling.cs

  Zip encryption (AES-128, AES-256, ZipCrypto):
    -> test/CodeBrix.Compression.Tests/Zip/ZipEncryptionHandling.cs

  AES encryption internals (transforms, streams, salt/block validation):
    -> test/CodeBrix.Compression.Tests/Encryption/EncryptionTests.cs

  Zip extra data fields:
    -> test/CodeBrix.Compression.Tests/Zip/ZipExtraDataHandling.cs

  Zip name transforms and path handling:
    -> test/CodeBrix.Compression.Tests/Zip/ZipNameTransformHandling.cs
    -> test/CodeBrix.Compression.Tests/Zip/WindowsNameTransformHandling.cs

  Zip string encoding:
    -> test/CodeBrix.Compression.Tests/Zip/ZipStringsTests.cs

  Zip corruption handling:
    -> test/CodeBrix.Compression.Tests/Zip/ZipCorruptionHandling.cs

  Zip passthrough operations:
    -> test/CodeBrix.Compression.Tests/Zip/PassthroughTests.cs

  Core zip test infrastructure (in-memory creation, data verification):
    -> test/CodeBrix.Compression.Tests/Zip/ZipTests.cs

  GZip compression/decompression, stream ownership, flushing, error handling:
    -> test/CodeBrix.Compression.Tests/GZip/GZipTests.cs

  Tar archives (create, read, extract, long names, encoding, async,
  entry properties, checksums, stream ownership, tar.gz integration):
    -> test/CodeBrix.Compression.Tests/Tar/TarTests.cs

  BZip2 compression/decompression:
    -> test/CodeBrix.Compression.Tests/BZip2/Bzip2Tests.cs

  Checksum operations (CRC-32, Adler32):
    -> test/CodeBrix.Compression.Tests/Checksum/

  LZW compression:
    -> test/CodeBrix.Compression.Tests/Lzw/

  Serialization:
    -> test/CodeBrix.Compression.Tests/Serialization/

HOW TO USE: Fetch the raw file content from GitHub using a URL like:
    https://raw.githubusercontent.com/ellisnet/CodeBrix.Compression/main/{path}
For example:
    https://raw.githubusercontent.com/ellisnet/CodeBrix.Compression/main/test/CodeBrix.Compression.Tests/Zip/FastZipHandling.cs

================================================================================

API QUICK REFERENCE
=====================

--- ZIP ---
ZipOutputStream(stream)           Create zip output stream
  .SetLevel(0-9)                  Set compression level
  .Password = "..."              Set password for encryption
  .PutNextEntry(entry)           Start writing an entry
  .CloseEntry()                  Finish writing current entry
  .Finish()                      Finalize the archive
  .IsStreamOwner                 Control underlying stream disposal

ZipInputStream(stream)            Create zip input stream
  .GetNextEntry()                Get next entry (null when done)
  .Password = "..."             Set password for decryption

ZipFile("path")                   Open zip for random access
ZipFile(stream)                   Open zip from stream
  .GetInputStream(entry)         Get stream for specific entry
  .Count                         Number of entries

ZipEntry("name")                  Create a zip entry
  .DateTime                      Modification date
  .AESKeySize                    0, 128, or 256
  .Size / .CompressedSize        File sizes
  .IsFile / .IsDirectory         Entry type

FastZip                           High-level convenience class
  .CreateZip(zip, dir, recurse, filter)
  .ExtractZip(zip, dir, filter)
  .Password                      Encryption password
  .EntryEncryptionMethod          ZipCrypto, AES128, AES256
  .CreateEmptyDirectories         Preserve empty dirs
  .RestoreDateTimeOnExtract       Preserve timestamps

--- GZIP ---
GZip.Compress(inStream, outStream, isStreamOwner)
GZip.Decompress(inStream, outStream, isStreamOwner)
GZipOutputStream(stream)          Compression stream
GZipInputStream(stream)           Decompression stream

--- TAR ---
TarArchive.CreateOutputTarArchive(stream)
TarArchive.CreateInputTarArchive(stream, nameEncoding)
  .WriteEntry(entry, recurse)
  .ExtractContents(path)
  .RootPath                      Base path for entries
  .IsStreamOwner

TarOutputStream(stream, nameEncoding)
TarOutputStream(stream, blockFactor, nameEncoding)
  .PutNextEntry(entry)
  .CloseEntry()

TarInputStream(stream, nameEncoding)
  .GetNextEntry()
  .GetNextEntryAsync(ct)

TarEntry.CreateTarEntry("name")   Create entry by name
TarEntry.CreateEntryFromFile(path) Create from file system

--- BZIP2 ---
BZip2.Compress(inStream, outStream, isStreamOwner, level)
BZip2.Decompress(inStream, outStream, isStreamOwner)
BZip2OutputStream(stream)
BZip2InputStream(stream)

--- CHECKSUMS ---
Crc32                             CRC-32 checksum
Adler32                           Adler-32 checksum
  .Update(buffer)                Add data
  .Value                         Get checksum value

================================================================================
