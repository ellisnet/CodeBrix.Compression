# CodeBrix.Compression

Create, read and extract Zip, GZip, Tar and BZip2 archives using .NET.

CodeBrix.Compression is a .NET library for working with compressed archives in multiple formats including Zip, GZip, Tar and BZip2. It supports encryption (AES-128 and AES-256), Zip64, and streaming operations for both creation and extraction of archives.

CodeBrix.Compression has no dependencies other than .NET, and is provided as a .NET 10 library and associated `CodeBrix.Compression.MitLicenseForever` NuGet package.

CodeBrix.Compression supports applications and assemblies that target Microsoft .NET version 10.0 and later. Microsoft .NET version 10.0 is a Long-Term Supported (LTS) version of .NET, and was released on Nov 11, 2025; and will be actively supported by Microsoft until Nov 14, 2028. Please update your C#/.NET code and projects to the latest LTS version of Microsoft .NET.

CodeBrix.Compression is a fork of the code of the popular SharpZipLib library version 1.4.2 - see below for licensing details.

## CodeBrix.Compression supports:

* Zip archives (create, read, update, extract)
* GZip compression and decompression
* Tar archives (create, read, extract)
* BZip2 compression and decompression
* AES-128 and AES-256 encryption for Zip archives
* Zip64 extensions for large files
* Streaming (non-seekable) input and output
* In-memory archive operations
* Checksums (CRC-32, Adler32, BZip2 CRC)
* Many more...

## Sample Code

### Create a Zip Archive

```csharp
using CodeBrix.Compression.Zip;

using var fileStream = File.Create("archive.zip");
using var zipStream = new ZipOutputStream(fileStream);

zipStream.SetLevel(9); // 0-9, 9 being the highest level of compression

var entry = new ZipEntry("document.txt")
{
    DateTime = DateTime.Now
};

zipStream.PutNextEntry(entry);

var buffer = File.ReadAllBytes("document.txt");
zipStream.Write(buffer, 0, buffer.Length);

zipStream.CloseEntry();
zipStream.Finish();
```

### Create an AES-Encrypted Zip Archive

```csharp
using CodeBrix.Compression.Zip;

using var fileStream = File.Create("encrypted.zip");
using var zipStream = new ZipOutputStream(fileStream);

zipStream.SetLevel(9);
zipStream.Password = "my-secret-password";

var entry = new ZipEntry("confidential.txt")
{
    AESKeySize = 256, // Use AES-256 encryption
    DateTime = DateTime.Now
};

zipStream.PutNextEntry(entry);

var buffer = File.ReadAllBytes("confidential.txt");
zipStream.Write(buffer, 0, buffer.Length);

zipStream.CloseEntry();
zipStream.Finish();
```

### Extract a Zip Archive

```csharp
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
```

### Read a Zip Archive with ZipFile

```csharp
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
```

### Create and Extract with FastZip

```csharp
using CodeBrix.Compression.Zip;

var fastZip = new FastZip
{
    CreateEmptyDirectories = true,
    Password = "optional-password",
    EntryEncryptionMethod = ZipEncryptionMethod.AES256
};

// Create a zip from a directory
fastZip.CreateZip("backup.zip", @"C:\MyFolder", recurse: true, fileFilter: null);

// Extract a zip to a directory
fastZip.ExtractZip("backup.zip", @"C:\Extracted", fileFilter: null);
```

### GZip Compression

```csharp
using CodeBrix.Compression.GZip;

// Compress a file
GZip.Compress(File.OpenRead("data.txt"), File.Create("data.txt.gz"), isStreamOwner: true);

// Decompress a file
GZip.Decompress(File.OpenRead("data.txt.gz"), File.Create("data.txt"), isStreamOwner: true);
```

### Tar Archive

```csharp
using CodeBrix.Compression.Tar;

// Create a tar archive
using var outStream = File.Create("archive.tar");
using var tarArchive = TarArchive.CreateOutputTarArchive(outStream);

var tarEntry = TarEntry.CreateEntryFromFile("document.txt");
tarArchive.WriteEntry(tarEntry, recurse: false);

tarArchive.Close();
```

### BZip2 Compression

```csharp
using CodeBrix.Compression.BZip2;

// Compress
BZip2.Compress(File.OpenRead("data.txt"), File.Create("data.txt.bz2"), isStreamOwner: true, level: 9);

// Decompress
BZip2.Decompress(File.OpenRead("data.txt.bz2"), File.Create("data.txt"), isStreamOwner: true);
```

Note that significant additional sample code is available in the `CodeBrix.Compression.Tests` project.

## License

The project is licensed under the MIT License. see: https://en.wikipedia.org/wiki/MIT_License

All code from SharpZipLib version 1.4.2 was licensed under the MIT License. This project (CodeBrix.Compression) complies with all provisions of the open source license of SharpZipLib version 1.4.2 (code) - and will make all modified, adapted and derived code incorporated into the CodeBrix.Compression library freely available as open source, under the same license as the SharpZipLib code license.
