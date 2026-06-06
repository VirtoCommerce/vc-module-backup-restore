angular.module('Virtocommerce.Backuprestore')
    .controller('Virtocommerce.Backuprestore.helloWorldController', ['$scope', 'Virtocommerce.Backuprestore.webApi', function ($scope, api) {
        var blade = $scope.blade;
        blade.title = 'BackupRestore';

        blade.refresh = function () {
            api.get(function (data) {
                blade.title = 'backup-restore.blades.hello-world.title';
                blade.data = data.result;
                blade.isLoading = false;
            });
        };

        blade.refresh();
    }]);
