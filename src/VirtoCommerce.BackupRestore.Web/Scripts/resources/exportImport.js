angular.module('platformWebApp')
.factory('platformWebApp.exportImport.resource', ['$resource', function ($resource) {

    return $resource(null, {},
        {
            getNewExportManifest: { url: 'api/platform/export/manifest/new' },
            runExport: { method: 'POST', url: 'api/platform/export' },

            loadExportManifest: { url: 'api/platform/export/manifest/load' },
            runImport: { method: 'POST', url: 'api/platform/import' },

            // List backups already in blob storage via the Assets REST API, so a restore can
            // reuse a file that's already there (a previous backup, or one copied into the
            // 'backups' folder out-of-band) WITHOUT re-uploading it through the proxy — which
            // sidesteps the Cloudflare/ingress request-body size cap. Returns { totalCount, results }.
            listBackups: { method: 'GET', url: 'api/assets' },

            sampleDataDiscover: { url: 'api/platform/sampledata/discover', isArray: true },
            importSampleData: { method: 'POST', url: 'api/platform/sampledata/import', params: { name: '@name' } },

            taskCancel: { method: 'POST', url: 'api/platform/exortimport/tasks/:jobId/cancel'}
        });
}]);
