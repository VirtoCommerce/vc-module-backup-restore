// Call this to register your module to main application
var moduleName = 'Virtocommerce.Backuprestore';

if (AppDependencies !== undefined) {
    AppDependencies.push(moduleName);
}

angular.module(moduleName, [])
    .config(['$stateProvider',
        function ($stateProvider) {
            $stateProvider
                .state('workspace.BackuprestoreState', {
                    url: '/backup-restore',
                    templateUrl: '$(Platform)/Scripts/common/templates/home.tpl.html',
                    controller: [
                        'platformWebApp.bladeNavigationService',
                        function (bladeNavigationService) {
                            var newBlade = {
                                id: 'blade1',
                                controller: 'Virtocommerce.Backuprestore.helloWorldController',
                                template: 'Modules/$(Virtocommerce.Backuprestore)/Scripts/blades/hello-world.html',
                                isClosingDisabled: true,
                            };
                            bladeNavigationService.showBlade(newBlade);
                        }
                    ]
                });
        }
    ])
    .run(['platformWebApp.mainMenuService', '$state',
        function (mainMenuService, $state) {
            //Register module in main menu
            var menuItem = {
                path: 'browse/backup-restore',
                icon: 'fa fa-cube',
                title: 'BackupRestore',
                priority: 100,
                action: function () { $state.go('workspace.BackuprestoreState'); },
                permission: 'backup-restore:access',
            };
            mainMenuService.addMenuItem(menuItem);
        }
    ]);
