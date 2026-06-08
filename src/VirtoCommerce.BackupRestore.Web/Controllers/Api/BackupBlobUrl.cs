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

            var normalized = relativePath.Replace('\\', '/').TrimStart('/');
            foreach (var segment in normalized.Split('/'))
            {
                if (segment == "..")
                {
                    throw new PlatformException($"Invalid path {relativePath}");
                }
            }

            var prefix = ModuleConstants.BackupBlobFolder + "/";
            var result = normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? normalized
                : prefix + normalized;

            var fileSegment = result.Substring(prefix.Length);
            if (fileSegment.Length == 0 || fileSegment.Contains('/'))
            {
                throw new PlatformException($"Invalid path {relativePath}");
            }

            return result;
        }
    }
}
