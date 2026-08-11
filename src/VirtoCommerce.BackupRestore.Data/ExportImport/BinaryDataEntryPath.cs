using System;
using System.Linq;

namespace VirtoCommerce.BackupRestore.Data.ExportImport;

internal static class BinaryDataEntryPath
{
    public const string Directory = "assets/";

    public static void Validate(string reference)
    {
        if (string.IsNullOrEmpty(reference)
            || !reference.StartsWith(Directory, StringComparison.Ordinal)
            || reference.Contains('\\'))
        {
            ThrowInvalidReference(reference);
        }

        var relativePath = reference[Directory.Length..];
        if (relativePath.Length == 0 || relativePath.Split('/').Any(IsInvalidSegment))
        {
            ThrowInvalidReference(reference);
        }
    }

    private static bool IsInvalidSegment(string segment)
    {
        return segment.Length == 0
            || segment is "." or ".."
            || segment.Any(char.IsControl);
    }

    private static void ThrowInvalidReference(string reference)
    {
        throw new ArgumentException($"The binary data reference '{reference}' is not a safe package path.", nameof(reference));
    }
}
