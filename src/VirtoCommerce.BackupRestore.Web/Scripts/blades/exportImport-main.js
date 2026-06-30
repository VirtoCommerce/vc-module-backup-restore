angular.module('platformWebApp')
.controller('platformWebApp.exportImport.mainController', ['$scope', 'platformWebApp.bladeNavigationService', 'platformWebApp.authService', function ($scope, bladeNavigationService, authService) {

    $scope.export = function () {
        $scope.selectedNodeId = 'export';

        var newBlade = {
            controller: 'platformWebApp.exportImport.exportMainController',
            template: 'Modules/$(VirtoCommerce.BackupRestore)/Scripts/blades/export-main.tpl.html'
        };
        bladeNavigationService.showBlade(newBlade, $scope.blade);
    };

    $scope.import = function () {
        if (authService.checkPermission('platform:backuprestore:restore')) {
            $scope.selectedNodeId = 'import';

            var newBlade = {
                controller: 'platformWebApp.exportImport.importMainController',
                template: 'Modules/$(VirtoCommerce.BackupRestore)/Scripts/blades/import-main.tpl.html'
            };
            bladeNavigationService.showBlade(newBlade, $scope.blade);
        }
    };

    // "Backup storage" option — only for users granted platform:backuprestore:storage.
    // Opens the Assets module's browser scoped to the backups folder (review / download /
    // cleanup), as a child blade — same approach as the Store module's assets widget.
    $scope.canViewBackupStorage = authService.checkPermission('platform:backuprestore:storage');

    $scope.backupStorage = function () {
        if (!$scope.canViewBackupStorage) {
            return;
        }
        $scope.selectedNodeId = 'backupStorage';

        var newBlade = {
            id: 'backupStorage',
            subtitle: 'platform.blades.exportImport-main.menu.storage.title',
            controller: 'virtoCommerce.assetsModule.assetListController',
            template: 'Modules/$(VirtoCommerce.Assets)/Scripts/blades/asset-list.tpl.html',
            // The Assets browser searches blob storage at currentEntity.url; point it at the
            // same 'backups' folder backups are written to / restored from.
            currentEntity: { url: 'backups' }
        };
        bladeNavigationService.showBlade(newBlade, $scope.blade);
    };

    $scope.blade.headIcon = 'fa fa-database';
    $scope.blade.isLoading = false;
}]);
