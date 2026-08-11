using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.BackupRestore.Core;
using VirtoCommerce.Platform.Core.ExportImport;

namespace VirtoCommerce.BackupRestore.Data.ExportImport;

internal sealed class BackupBinaryDataWriter(IZipBackupArchive archive) : IExportBinaryDataWriter
{
    private readonly HashSet<string> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedEntries = new(StringComparer.Ordinal);

    public async Task WriteAsync(string reference, Stream sourceStream, CancellationToken cancellationToken)
    {
        BinaryDataEntryPath.Validate(reference);
        ArgumentNullException.ThrowIfNull(sourceStream);
        cancellationToken.ThrowIfCancellationRequested();

        if (_failedEntries.Contains(reference))
        {
            throw new InvalidDataException($"Binary data entry '{reference}' could not be written earlier in this export.");
        }

        if (!_entries.Add(reference))
        {
            return;
        }

        try
        {
            await using var entryStream = await archive.CreateEntryAsync(reference);
            await sourceStream.CopyToAsync(entryStream, cancellationToken);
        }
        catch
        {
            _failedEntries.Add(reference);
            throw;
        }
    }
}
