using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.BackupRestore.Core;
using VirtoCommerce.BackupRestore.Data;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.BackupRestore.Web;

public class Module : IModule
{
    public ManifestModuleInfo ModuleInfo { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        // Single implementation, exposed under both the modern module interface and — for the
        // deprecation period — the obsolete platform interface, so existing consumers that resolve
        // IPlatformExportImportManager keep working unchanged (no breaking changes).
        serviceCollection.AddScoped<BackupRestoreManager>();
        serviceCollection.AddScoped<IBackupRestoreManager>(sp => sp.GetRequiredService<BackupRestoreManager>());
#pragma warning disable VC0014 // IPlatformExportImportManager is obsolete; kept for backward compatibility.
        serviceCollection.AddScoped<IPlatformExportImportManager>(sp => sp.GetRequiredService<BackupRestoreManager>());
#pragma warning restore VC0014
        // Fully-qualified: IZipBackupArchiveFactory moved into this module but the referenced
        // Platform.Core NuGet still ships a copy, so the simple name would be ambiguous.
        serviceCollection.AddSingleton<Core.IZipBackupArchiveFactory, SharpZipBackupArchiveFactory>();
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.ApplicationServices;

        // Register permissions under the "Platform" group with the SAME string values the platform
        // used before, so existing role assignments continue to grant access.
        var permissionsRegistrar = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
        permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "Platform", ModuleConstants.Security.Permissions.AllPermissions);
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
