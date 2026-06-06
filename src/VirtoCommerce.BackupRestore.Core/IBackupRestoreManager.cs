using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.ExportImport;

namespace VirtoCommerce.BackupRestore.Core;

/// <summary>
/// Modern, module-owned contract for platform backup (export) and restore (import).
/// <para>
/// This supersedes the obsolete <see cref="VirtoCommerce.Platform.Core.ExportImport.IPlatformExportImportManager"/>.
/// The reusable contract DTOs (<see cref="PlatformExportManifest"/>, <see cref="ExportImportProgressInfo"/>,
/// <see cref="IExportSupport"/>, <see cref="IImportSupport"/>, push notifications, …) intentionally remain in
/// <c>VirtoCommerce.Platform.Core</c> so the dozens of modules that implement export/import participation are not
/// broken by this extraction. Only the implementation moved into this module.
/// </para>
/// <para>
/// For a deprecation period the implementation also satisfies the obsolete platform interface, so existing
/// consumers that resolve <c>IPlatformExportImportManager</c> keep working unchanged.
/// </para>
/// </summary>
public interface IBackupRestoreManager
{
    PlatformExportManifest GetNewExportManifest(string author);

    PlatformExportManifest ReadExportManifest(Stream stream);

    Task ExportAsync(Stream outStream, PlatformExportManifest exportOptions, Action<ExportImportProgressInfo> progressCallback, CancellationToken cancellationToken);

    Task ImportAsync(Stream inputStream, PlatformExportManifest importOptions, Action<ExportImportProgressInfo> progressCallback, CancellationToken cancellationToken);
}
