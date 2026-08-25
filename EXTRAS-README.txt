================================================================================
EXTRAS-README: CodeBrix.Compression
Samples, tools and other content in this repository that is not part of a NuGet package
================================================================================

This repository contains no sample applications, demo projects, tools or
optional test-data downloads. It builds one library and one test project, and
nothing else.

TEST PROJECT
============
The only non-package content is the test project:

    tests/CodeBrix.Compression.Tests/

It is an xUnit v3 project covering Zip, Tar, GZip, BZip2, checksums, DCL, LZW,
Zip encryption, the raw Deflate/Inflate layer, the Core name/path filter and
scanning helpers, and exception serialization. Its files double as the worked
examples that AGENT-README.txt links to under "WORKING EXAMPLES ON GITHUB".

How to run it:

    dotnet test CodeBrix.Compression.sln

No environment variables, downloads or fixtures are required. See
MAINTAINER-README.txt for the one soft dependency (an optional 7-Zip binary used
to cross-verify a few archives, which is skipped when 7-Zip is not installed).

OPTIONAL EXTERNAL TOOL
======================
7-Zip (`7z` or `7za`) is looked up on PATH, and in the default Windows install
locations, by tests/CodeBrix.Compression.Tests/TestSupport/SevenZip.cs. It is
not required: when no binary is found the cross-verification step is skipped
with a message and the tests still pass. Installing 7-Zip simply widens the
verification a little.

A NOTE ON THE UNTRACKED "test/" FOLDER
======================================
A `test/` (singular) folder may appear in a working copy. It is stale build
output, is not tracked by git, and holds no source or documentation. The tracked
test project is `tests/` (plural).

================================================================================
