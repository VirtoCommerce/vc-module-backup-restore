using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace Virtocommerce.Backuprestore.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public static class Permissions
        {
            public const string Access = "backup-restore:access";
            public const string Create = "backup-restore:create";
            public const string Read = "backup-restore:read";
            public const string Update = "backup-restore:update";
            public const string Delete = "backup-restore:delete";

            public static string[] AllPermissions { get; } =
            [
                Access,
                Create,
                Read,
                Update,
                Delete,
            ];
        }
    }

    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor BackuprestoreEnabled { get; } = new()
            {
                Name = "Backuprestore.Enabled",
                GroupName = "BackupRestore|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return BackuprestoreEnabled;
                }
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings;
            }
        }
    }
}
