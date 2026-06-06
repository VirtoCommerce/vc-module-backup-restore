namespace VirtoCommerce.BackupRestore.Core;

public static class ModuleConstants
{
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
