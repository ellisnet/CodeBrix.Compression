================================================================================
MAINTAINER-README: CodeBrix.Compression
Notes for people and agents MAINTAINING this repository — not for package consumers
================================================================================

If you are consuming the NuGet package, stop reading and open AGENT-README.txt
instead. This file is about the repository itself.

PURPOSE AND SCOPE
=================
This repository produces exactly one NuGet package:

    CodeBrix.Compression.MitLicenseForever
        Assembly / root namespace: CodeBrix.Compression
        Consumer documentation:    AGENT-README.txt (repo root)
        Project:                   src/CodeBrix.Compression/CodeBrix.Compression.csproj

There are no sibling packages, no native assets and no platform-specific heads.

REPOSITORY LAYOUT
=================

    CodeBrix.Compression.sln          Solution (Solution Items + Tests folder).
    icon-codebrix-128.png             Package icon; packed from the repo root.
    LICENSE                           MIT.
    THIRD-PARTY-NOTICES.txt           Upstream notices; packed into the nupkg.
    README.md                         Human-facing overview; also the nuspec readme.
    AGENT-README.txt                  Consumer documentation; packed into the nupkg.
    MAINTAINER-README.txt             This file (not packed).
    EXTRAS-README.txt                 Non-package content (not packed).
    README-INDEX.txt                  Map of the README files (not packed).

    src/CodeBrix.Compression/
        CodeBrix.Compression.csproj   The one packable project.
        CompressionOptions.cs         Global behavior switches (root namespace).
        InternalsVisibleTo.cs         Grants internals to the .Tests project.
        Zip/                          ZipFile, ZipEntry, Zip streams, FastZip,
                                      name transforms, extra data, and the
                                      Zip/Compression + Zip/Compression/Streams
                                      raw Deflate/Inflate layer.
        Core/                         Name/path filters, file-system scanning,
                                      name transforms, stream and path helpers,
                                      Core/Exceptions/CompressionExceptionBase.
        Tar/                          TarArchive, TarEntry, Tar streams, buffer.
        GZip/                         GZip static helper and GZip streams.
        BZip2/                        BZip2 static helper and BZip2 streams.
        Checksum/                     Crc32, Adler32, BZip2 CRC.
        Encryption/                   ZipAESTransform, ZipAESStream.
        Dcl/                          PKWARE DCL "implode" decoder.
        Lzw/                          LzwInputStream (.Z decompression).

    tests/CodeBrix.Compression.Tests/
        The single test project, grouped by feature folder (Zip, Tar, GZip,
        BZip2, Checksum, Dcl, Lzw, Encryption, Base, Core, Serialization) with
        shared helpers under TestSupport/.

NOTE ON THE UNTRACKED "test/" FOLDER: a `test/` (singular) directory may exist
in a working copy as a leftover build-output location. It is NOT tracked by git
and contains only obj/bin artifacts. The real, tracked test project is
`tests/CodeBrix.Compression.Tests` (plural), which is what the solution
references and what the GitHub links in AGENT-README.txt point at. If you find a
`test/` folder locally, it can be deleted; never add documentation or source to
it.

BUILDING
========

    dotnet restore CodeBrix.Compression.sln
    dotnet build CodeBrix.Compression.sln

Target framework is net10.0 only - never multi-target and never add an older
TFM. The library is fully managed with zero PackageReference dependencies; keep
it that way, since "no dependencies beyond .NET" is a documented consumer-facing
property of the package.

TESTING
=======

    dotnet test CodeBrix.Compression.sln

The test project uses xUnit v3 with Microsoft.NET.Test.Sdk and
xunit.runner.visualstudio. There are no opt-in environment variables and no
special prep: every test is self-contained and uses temporary files/streams.

One soft dependency: tests/CodeBrix.Compression.Tests/TestSupport/SevenZip.cs
probes for a 7-Zip binary (`7z` or `7za` on PATH, or the default Windows install
locations) so a few tests can cross-verify archives with an external tool. When
no 7-Zip binary is found the helper reports that fact rather than failing, so
7-Zip is a convenience, not a requirement.

PACKAGING AND PUBLISHING
========================
Packing is driven entirely from src/CodeBrix.Compression/CodeBrix.Compression.csproj:

  - GeneratePackageOnBuild is true, so every build produces a fresh .nupkg.
  - PackageId is CodeBrix.Compression.MitLicenseForever; Product/Title are
    CodeBrix.Compression.
  - PackageLicenseExpression is MIT and PackageRequireLicenseAcceptance is true.
  - Files packed from the repo root: icon-codebrix-128.png (PackageIcon),
    README.md (PackageReadmeFile), AGENT-README.txt and THIRD-PARTY-NOTICES.txt.
    AGENT-README.txt is the ONLY README variant that ships in the package;
    MAINTAINER-README.txt, EXTRAS-README.txt and README-INDEX.txt do not.

VERSIONING SCHEME: date-stamped and auto-incrementing, 1.<x>.<y>.<z>, where x is
whole years since the _VersionBaseYear property, y is the UTC day of year
(1-based), and z is the UTC minute of day (0-1439). Every build therefore yields
a new, strictly increasing version, and two builds within the same UTC minute
produce the same version - so do not publish twice inside one minute. This is
not SemVer: major is pinned and minor encodes the year, so neither signals API
compatibility. Re-baseline by changing _VersionBaseYear in the csproj.

Never write version numbers into AGENT-README.txt; they go stale immediately
under this scheme.

PROVENANCE AND VENDORED SOURCES
===============================
Two upstream sources are incorporated, both documented in THIRD-PARTY-NOTICES.txt:

  1. SharpZipLib (ICSharpCode.SharpZipLib) 1.4.2, MIT. Nearly the whole library
     derives from it. Every namespace was rewritten from ICSharpCode.SharpZipLib
     to CodeBrix.Compression. When porting a fix from upstream, keep the rename
     and keep the API shape - consumers are told the surface mirrors upstream.
     Note the licensing history: SharpZipLib before its relicensing was GPLv2
     with a linking exception; 1.4.2, the version this fork is derived from, is
     MIT.

  2. blast (zlib contrib) 1.3 by Mark Adler, zlib license - the reference
     decoder for the PKWARE DCL "implode" format, ported to C# in
     src/CodeBrix.Compression/Dcl/. The zlib license requires altered versions
     to be plainly marked as such; the Dcl source files carry that marking.
     Do not remove it.

If either upstream is refreshed, update THIRD-PARTY-NOTICES.txt (including the
version-used line) in the same change.

CODING CONVENTIONS
==================
This repository follows the CodeBrix family conventions:

  - net10.0 only. Never multi-target, never add an older TFM.
  - Nullable reference types are OFF. Do not add <Nullable>enable</Nullable>, do
    not write "?" on reference types, and do not use the null-forgiving "!"
    operator. Value-type nullables (int?, bool?) are fine.
  - No project-level warning suppression.
  - Source is organized into feature sub-folders whose names match the
    namespaces (Zip/, Tar/, GZip/, BZip2/, Checksum/, Core/, Dcl/, Lzw/,
    Encryption/); entry-point types for the root namespace live at the project
    root.
  - InternalsVisibleTo.cs grants internals to CodeBrix.Compression.Tests; tests
    exercise internal helpers (for example the byte-order stream extensions)
    through it.
  - Because much of the source is ported from upstream, its formatting and
    naming deviate in places from newer CodeBrix libraries. Prefer minimal,
    surgical edits in ported files so upstream fixes stay easy to apply.

NOTES
=====
  - Documentation split: AGENT-README.txt is consumer-only (what the package
    does, how to reference it, API and pitfalls). Build/test/pack/versioning and
    vendored-source notes belong here; samples and non-package content belong in
    EXTRAS-README.txt.
  - AGENT-README.txt links to test files by GitHub URL under
    tests/CodeBrix.Compression.Tests/. If a test file is renamed or moved, fix
    those links in the same change.

================================================================================
