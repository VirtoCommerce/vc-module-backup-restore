angular.module('Virtocommerce.Backuprestore')
    .factory('Virtocommerce.Backuprestore.webApi', ['$resource', function ($resource) {
        return $resource('api/backup-restore');
    }]);
