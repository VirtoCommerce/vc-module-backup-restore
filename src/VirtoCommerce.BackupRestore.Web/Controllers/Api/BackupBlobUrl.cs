using System;
using VirtoCommerce.BackupRestore.Core;
using VirtoCommerce.Platform.Core.Exceptions;

namespace VirtoCommerce.BackupRestore.Web.Controllers.Api
{
    /// <summary>
    /// Resolves a user-supplied value to a blob url confined to the backups folder
    /// (<see cref="ModuleConstants.BackupBlobFolder"/>), rejecting directory traversal.
    /// </summary>
    public static class BackupBlobUrl
    {
        /// <summary>
        /// Two input shapes are accepted:
        /// <list type="bullet">
        /// <item>a bare file name — the export-download / sample-data paths, where the server owns the name;</item>
        /// <item>a relative url already rooted at the backups folder — the import / manifest-load paths,
        /// where the value is the relativeUrl returned by the asset upload.</item>
        /// </list>
        /// Either way the result must denote a single file DIRECTLY inside the backups folder: nested
        /// descendants are rejected so this API can't be used to read arbitrary blobs.
        /// </summary>
        public static string GetSafe(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                throw new PlatformException("File name is required");
            }

            var prefix = ModuleConstants.BackupBlobFolder + "/";

            // Build the returned value from the ORIGINAL (still-encoded) input so it round-trips
            // through the provider's single percent-decode unchanged. Re-encoding or returning a
            // decoded form would double-decode a filename that legitimately contains '%'.
            var raw = relativePath.Replace('\\', '/').TrimStart('/');
            var result = raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? raw : prefix + raw;

            // Validate against the DECODED value: blob providers percent-decode the url before
            // resolving it to a real path (FileSystem via Uri.UnescapeDataString, Azure via
            // HttpUtility.UrlDecode), so a check against the encoded string would miss an encoded
            // separator such as "%2F" that decodes into a traversal (e.g.
            // "a%2F..%2F..%2Fsecret.zip" -> "a/../../secret.zip", escaping the backups folder).
            var decoded = Uri.UnescapeDataString(result).Replace('\\', '/');
            foreach (var segment in decoded.Split('/'))
            {
                if (segment == "..")
                {
                    throw new PlatformException($"Invalid path {relativePath}");
                }
            }

            // The result must denote a single file DIRECTLY inside the backups folder.
            var fileSegment = decoded.Substring(prefix.Length);
            if (fileSegment.Length == 0 || fileSegment.Contains('/'))
            {
                throw new PlatformException($"Invalid path {relativePath}");
            }

            return result;
        }
    }
}
