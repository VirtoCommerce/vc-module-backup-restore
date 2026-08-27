using System.IO;

namespace VirtoCommerce.BackupRestore.Data.ExportImport;

internal static class TemporaryFileStream
{
    private const int BufferSize = 81920;

    public static FileStream Create()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new FileStreamOptions
        {
            Access = FileAccess.ReadWrite,
            BufferSize = BufferSize,
            Mode = FileMode.CreateNew,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose,
            Share = FileShare.None,
        };

        return new FileStream(path, options);
    }
}
