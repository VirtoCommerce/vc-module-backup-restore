using System.IO;
using VirtoCommerce.BackupRestore.Core;

namespace VirtoCommerce.BackupRestore.Data;

public class SharpZipBackupArchiveFactory : IZipBackupArchiveFactory
{
    public virtual IZipBackupArchive CreateForWriting(Stream output, string password)
        => new SharpZipBackupArchive(output, password);

    public virtual IZipBackupArchive OpenForReading(Stream input, string password)
        => new SharpZipBackupArchive(input, password, _reader: true);
}
