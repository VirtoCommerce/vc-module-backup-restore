namespace VirtoCommerce.BackupRestore.Core;

public static class ModuleConstants
{
    // Blob storage folder (in the Assets module's configured store) where backup ZIPs are kept.
    // Using shared blob storage instead of the local file system lets any instance read a backup
    // written by any other instance, which is required for multi-instance / load-balanced setups.
    public const string BackupBlobFolder = "backups";

    public static class Security
    {
        public static class Permissions
        {
            // NOTE: these permission string values are intentionally identical to the ones the
            // platform used to register (VirtoCommerce.Platform.Core.PlatformConstants.Security.Permissions).
            // Keeping the exact strings means existing role assignments keep working after the
            // feature moves out of the platform into this module — no breaking changes.
            public const string Access = "platform:exportImport:access";
            public const string Export = "platform:export";
            public const string Import = "platform:import";

            public static string[] AllPermissions { get; } =
            [
                Access,
                Export,
                Import,
            ];
        }
    }
}
