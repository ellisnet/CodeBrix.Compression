using System.IO;
using CodeBrix.Compression.Core;

namespace CodeBrix.Compression.Tar;

internal static class TarStringExtension
{
    public static string ToTarArchivePath(this string s)
    {
        return PathUtils.DropPathRoot(s).Replace(Path.DirectorySeparatorChar, '/');
    }
}