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
            public const string Access = "platform:backuprestore:access";
            public const string Backup = "platform:backuprestore:backup";
            public const string Restore = "platform:backuprestore:restore";

            // Grants access to the "Backup storage" menu item, which opens the Assets browser
            // scoped to the backups folder so admins can review / download / clean up backups.
            public const string Storage = "platform:backuprestore:storage";

            // Legacy permission strings that the platform used to register before the export/import
            public const string Export = "platform:export";
            public const string Import = "platform:import";

            public static string[] AllPermissions { get; } =
            [
                Access,
                Backup,
                Restore,
                Storage,
                Export,
                Import,
            ];
        }
    }
}
