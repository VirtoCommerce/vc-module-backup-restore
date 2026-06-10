// Register this module with the main platform application.
// The actual UI (states, menu item, blades, widgets, push-notification templates) is
// registered against the 'platformWebApp' module by exportImport.js and its blades —
// kept verbatim from the platform so menu state names, blade ids and localization keys
// are preserved (no breaking changes).
var moduleName = 'VirtoCommerce.BackupRestore';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, []);
