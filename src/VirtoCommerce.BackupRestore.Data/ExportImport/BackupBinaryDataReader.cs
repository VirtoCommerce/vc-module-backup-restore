using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.BackupRestore.Core;
using VirtoCommerce.Platform.Core.ExportImport;

namespace VirtoCommerce.BackupRestore.Data.ExportImport;

internal sealed class BackupBinaryDataReader(IZipBackupArchive archive) : IImportBinaryDataReader
{
    public Task<Stream> OpenReadAsync(string reference, CancellationToken cancellationToken)
    {
        BinaryDataEntryPath.Validate(reference);
        cancellationToken.ThrowIfCancellationRequested();

        return archive.OpenEntryAsync(reference);
    }
}
