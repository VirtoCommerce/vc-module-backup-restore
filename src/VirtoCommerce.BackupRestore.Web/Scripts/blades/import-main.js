angular.module('platformWebApp')
    .controller('platformWebApp.exportImport.importMainController', [
        '$scope',
        '$timeout',
        '$translate',
        'platformWebApp.bladeNavigationService',
        'platformWebApp.exportImport.resource',
        'platformWebApp.authService',
        'platformWebApp.exportImport.progressService',
        'FileUploader',
        function ($scope, $timeout, $translate, bladeNavigationService, exportImportResourse, authService, progressService, FileUploader) {
        var blade = $scope.blade;
        blade.updatePermission = 'platform:backuprestore:restore';
        blade.headIcon = 'fa fa-download';
        blade.title = 'platform.blades.import-main.title';
        blade.isLoading = false;
        $scope.importRequest = {};
        // Surfaced in the sensitive-data warning banner so the admin sees exactly which account
        // the backend will preserve during the user-import phase. Matches PlatformExportManifest.CallerUserName.
        $scope.currentUserName = authService.userName;
        // Wire up shared progress UI helpers (copy*, progressItems/Stats, detailed-log toggle).
        progressService.attach($scope, blade, 'import');

        $scope.passwordError = false;
        $scope.clearPasswordError = function () { $scope.passwordError = false; };

        // --- Large-file upload safety net -------------------------------------------------
        // The whole backup is uploaded to blob storage (api/assets) in a single multipart
        // POST BEFORE any restore job is queued. For large files (~100 MB+) that request can
        // be silently held or dropped upstream — most commonly at the Cloudflare edge, whose
        // request-body cap on non-Enterprise plans is 100 MB. When that happens the browser
        // sends every byte (progress reaches 100 %) but no response ever comes back, so the
        // uploader's onSuccessItem/onErrorItem never fire and the blade used to sit on an
        // indeterminate "uploading" bar forever. The state machine below distinguishes
        // bytes-in-flight from waiting-on-the-server, and a no-activity watchdog converts an
        // indefinite hang into an explicit, actionable error.

        // Treat the request as stalled when no upload activity is observed within this window.
        // Progress events fire continuously while bytes move (so a genuinely slow upload keeps
        // resetting the timer); they stop once the body is fully sent, so this also bounds the
        // "100 % uploaded, server never responded" hang. Kept comfortably above Cloudflare's
        // 100 s origin-response timeout so a working-but-slow backend isn't killed prematurely.
        var UPLOAD_INACTIVITY_TIMEOUT_MS = 120000;

        // null | 'uploading' (bytes in flight) | 'processing' (all bytes sent, awaiting response)
        $scope.uploadPhase = null;
        $scope.uploadProgress = 0;
        $scope.uploadFileName = null;

        var uploadWatchdog = null;
        var activeUploadItem = null;
        // Set when the user (or the watchdog) aborts, so onCancelItem doesn't clobber the
        // explanatory error the watchdog already showed, and a user-cancel stays silent.
        var uploadAbortedByWatchdog = false;

        function cancelUploadWatchdog() {
            if (uploadWatchdog) {
                $timeout.cancel(uploadWatchdog);
                uploadWatchdog = null;
            }
        }

        function armUploadWatchdog() {
            cancelUploadWatchdog();
            uploadWatchdog = $timeout(function () {
                uploadAbortedByWatchdog = true;
                if (activeUploadItem) {
                    try { $scope.uploader.cancelItem(activeUploadItem); } catch (e) { /* already gone */ }
                }
                var key = $scope.uploadProgress >= 100
                    ? 'platform.blades.import-main.errors.upload-no-response'
                    : 'platform.blades.import-main.errors.upload-stalled';
                resetUploadState();
                setUploadError($translate.instant(key));
            }, UPLOAD_INACTIVITY_TIMEOUT_MS);
        }

        function resetUploadState() {
            cancelUploadWatchdog();
            $scope.uploadPhase = null;
            $scope.uploadProgress = 0;
            activeUploadItem = null;
            blade.isLoading = false;
        }

        function setUploadError(message) {
            // Surface the failed file in the template's "upload-failed" row (it binds to
            // importRequest.fileName), and the reason via the standard blade error channel.
            $scope.importRequest.fileName = $scope.uploadFileName;
            bladeNavigationService.setError(message, blade);
        }

        // Map a failed upload's HTTP status to a specific, actionable message. 413 is the
        // direct symptom of exceeding the request-body size limit (Cloudflare edge, ingress
        // client_max_body_size, or Kestrel MaxRequestBodySize); the others cover the common
        // proxy / connectivity failures so the user never sees a bare status code.
        function describeUploadError(status, response) {
            var t = function (k) { return $translate.instant('platform.blades.import-main.errors.' + k); };
            switch (status) {
                case 413: return t('upload-too-large');
                case 522:
                case 524:
                case 504: return t('upload-gateway-timeout');
                case 502:
                case 503: return t('upload-gateway-unavailable');
                case 401:
                case 403: return t('upload-unauthorized');
            }
            if (!status) { return t('upload-network'); }
            var serverMsg = response && response.message ? response.message : null;
            return serverMsg || (t('upload-failed-status') + ' ' + status);
        }

        // User-initiated cancel of an in-flight upload (button in the progress UI).
        $scope.cancelUpload = function () {
            uploadAbortedByWatchdog = false;
            if (activeUploadItem) {
                try { $scope.uploader.cancelItem(activeUploadItem); } catch (e) { /* already gone */ }
            }
            resetUploadState();
            // User asked to stop — return quietly to the drop zone, no error banner.
            bladeNavigationService.setError(null, blade);
        };

        $scope.$on('$destroy', function () {
            cancelUploadWatchdog();
        });

        // --- Restore from a backup already in blob storage --------------------------------
        // Lets the admin pick a file that's already in the 'backups' folder instead of
        // uploading one through the browser. Because the bytes never leave the server side,
        // this bypasses the Cloudflare/ingress request-body size cap entirely — the workaround
        // for backups larger than the plan's upload limit (Free/Pro 100 MB, Business 200 MB,
        // Enterprise 500 MB). Files can be there from a previous backup, or copied in
        // out-of-band (e.g. straight into blob storage). Listing uses the Assets REST API.
        $scope.existingBackups = [];
        $scope.backupsLoading = false;
        // Description toggle (help-icon pattern, mirrors the password hint).
        $scope.existingBackupsDescrVisible = false;
        // Client-side filter — the folder listing is NOT paged server-side (the Assets folder
        // GET returns the whole folder; take/skip are ignored), so for large folders we filter
        // and scroll on the client instead of paginating.
        $scope.backupFilter = '';

        function loadExistingBackups() {
            $scope.backupsLoading = true;
            exportImportResourse.listBackups({ folderUrl: 'backups' },
                function (data) {
                    var entries = (data && data.results) || [];
                    $scope.existingBackups = _.chain(entries)
                        .filter(function (e) {
                            // Only real files (skip sub-folders) that look like a backup archive.
                            return e.type === 'blob' && e.name && e.name.toLowerCase().endsWith('.zip');
                        })
                        .sortBy(function (e) { return e.modifiedDate; })
                        .reverse() // newest first
                        .value();
                    $scope.backupsLoading = false;
                },
                function () {
                    // Non-fatal: the upload path still works. Fall back to the empty state.
                    $scope.existingBackups = [];
                    $scope.backupsLoading = false;
                });
        }

        $scope.refreshExistingBackups = loadExistingBackups;

        // Revert from the "Restore data information" step back to file selection: drop the
        // resolved manifest and the chosen file so the drop zone + existing-backups picker
        // come back, letting the user pick a different backup without reopening the blade.
        $scope.revertToFileSelection = function () {
            bladeNavigationService.setError(null, blade);
            $scope.passwordError = false;
            $scope.importRequest.exportManifest = null;
            $scope.importRequest.fileUrl = null;
            $scope.importRequest.fileName = null;
            $scope.importRequest.password = '';
            $scope.importRequest.modules = [];
            $scope.importRequest.handleSecurity = false;
            $scope.importRequest.handleBinaryData = false;
            $scope.importRequest.handleSettings = false;
            $scope.importRequest.handleDynamicProperties = false;
            loadExistingBackups();
        };

        // Pick a backup that's already in storage. Downstream flow is identical to a finished
        // upload: resolve the manifest, then let the user choose what to restore.
        $scope.selectExistingBackup = function (file) {
            loadManifestForBackup(file.relativeUrl, file.name);
        };

        // Shared by both entry points (finished upload and existing-file pick): load the
        // backup's manifest and pre-select everything it contains. `fileUrl` is the blob's
        // relative url (e.g. "/backups/<name>"); the backend confines it to the backups folder.
        function loadManifestForBackup(fileUrl, fileName) {
            bladeNavigationService.setError(null, blade);
            blade.isLoading = true;
            $scope.importRequest.fileUrl = fileUrl;
            $scope.importRequest.fileName = fileName;
            exportImportResourse.loadExportManifest({ fileUrl: fileUrl }, function (data) {
                // select all available data for import
                $scope.importRequest.handleSecurity = data.handleSecurity;
                $scope.importRequest.handleSettings = data.handleSettings;
                $scope.importRequest.handleBinaryData = data.handleBinaryData;
                $scope.importRequest.handleDynamicProperties = data.handleDynamicProperties;

                _.each(data.modules, function (x) { x.isChecked = true; });

                $scope.importRequest.exportManifest = data;
                $scope.updateModuleSelection();
                blade.isLoading = false;
            }, function (response) {
                // Manifest read failed (file missing/404, unauthorized, network, or the zip is
                // corrupt / not a Virto Commerce backup). Without this the blade would stay
                // stuck on the loading spinner with no message — most likely when picking a
                // stale file from the storage list. Clear loading, keep the file name visible
                // in the error row, and surface a specific reason so the user can retry or pick
                // another backup. exportManifest stays null, so the drop zone + picker remain.
                blade.isLoading = false;
                $scope.importRequest.fileName = fileName;
                bladeNavigationService.setError(describeManifestError(response && response.status, response && response.data), blade);
            });
        }

        // Plain-language reason for a failed manifest load, mapped from the HTTP status.
        function describeManifestError(status, data) {
            var t = function (k) { return $translate.instant('platform.blades.import-main.errors.' + k); };
            if (status === 404) { return t('manifest-not-found'); }
            if (status === 401 || status === 403) { return t('upload-unauthorized'); }
            if (!status) { return t('upload-network'); }
            // 4xx/5xx: prefer the backend's own message (e.g. a PlatformException), else generic.
            var serverMsg = data && data.message ? data.message : null;
            return serverMsg || t('manifest-load-failed');
        }

        $scope.$on("new-notification-event", function (event, notification) {
            if (!blade.notification || notification.id !== blade.notification.id) {
                return;
            }

            // Intercept wrong-password failures BEFORE the generic "Import error" path: those
            // are recoverable (the file uploaded fine, only decrypt failed), so we keep the
            // user in the password-form state instead of flipping into the "Upload failed"
            // / blade.error UI which would otherwise duplicate the file row on the blade.
            if (notification.finished && notification.errors && notification.errors.length > 0) {
                var hasPasswordError = _.any(notification.errors, function (e) {
                    return typeof e === 'string' && e.toLowerCase().indexOf('invalid backup password') !== -1;
                });
                if (hasPasswordError) {
                    $scope.passwordError = true;
                    blade.notification = null;
                    bladeNavigationService.setError(null, blade);
                    $scope.switchCommandButton("start");
                    blade.isLoading = false;
                    // Drop the wrong password from the request so it doesn't get re-sent
                    // unmodified on a quick second click — and so a stale value isn't kept
                    // around in scope longer than it has to be.
                    $scope.importRequest.password = '';
                    return;
                }
            }

            if (notification.jobId && notification.finished) {
                $scope.switchCommandButton("close");
            }
            angular.copy(notification, blade.notification);
            progressService.parseProgressLog($scope, blade);
            if (notification.errorCount > 0) {
                bladeNavigationService.setError('Import error', blade);
            }
        });

        $scope.canStartProcess = function () {
            var hasAnySection = _.any($scope.importRequest.modules) || $scope.importRequest.handleSecurity || $scope.importRequest.handleSettings || $scope.importRequest.handleBinaryData || $scope.importRequest.handleDynamicProperties;
            // Disable the start button until a password is entered for encrypted backups —
            // otherwise the request would fail at the first decrypt and the admin would have
            // to retry. Catching it pre-submit is cheaper and clearer.
            var hasRequiredPassword = !($scope.importRequest.exportManifest && $scope.importRequest.exportManifest.isEncrypted)
                || !!$scope.importRequest.password;
            return blade.hasUpdatePermission() && hasAnySection && hasRequiredPassword;
        }

        $scope.startProcess = function () {
            blade.isLoading = true;
            $scope.passwordError = false;

            exportImportResourse.runImport($scope.importRequest, function (data) {
                blade.notification = data;
                blade.isLoading = false;
            });

            $scope.switchCommandButton("cancel");
        }

        $scope.switchCommandButton = function (state) {
            const stateCommandIndex = blade.toolbarCommands.findIndex(x => x.target === 'import');

            if (state === "cancel") {
                blade.toolbarCommands[stateCommandIndex] = commandCancel;
            } else if (state === "close") {
                blade.toolbarCommands[stateCommandIndex] = commandClose;
            } else if (state === "start") {
                blade.toolbarCommands[stateCommandIndex] = commandStart;
            }
        }

        var commandCancel = {
            name: 'platform.commands.cancel',
            icon: 'fa fa-times',
            canExecuteMethod: function () {
                return blade.notification && !blade.notification.finished;
            },
            executeMethod: function () {
                exportImportResourse.taskCancel({ jobId: blade.notification.jobId }, null, null);
            },
            target: 'import'
        };

        var commandClose = {
            name: 'Close',
            icon: 'fa fa-times',
            canExecuteMethod: function () { return blade.error || blade.notification && blade.notification.finished; },
            executeMethod: function () { bladeNavigationService.closeBlade(blade); },
            target: 'import'
        };

        $scope.updateModuleSelection = function () {
            var selection = _.where($scope.importRequest.exportManifest.modules, { isChecked: true });
            $scope.importRequest.modules = _.pluck(selection, 'id');
        };

        if (!$scope.uploader) {
            // create the uploader
            // Upload to shared blob storage (Assets module) under the backups folder, instead of the
            // instance-local upload folder, so the restore background job can read the file on any
            // instance in a multi-instance deployment.
            var uploader = $scope.uploader = new FileUploader({
                scope: $scope,
                url: 'api/assets?folderUrl=backups',
                method: 'POST',
                autoUpload: true,
                removeAfterUpload: true
            });

            // ADDING FILTERS
            // zip only
            uploader.filters.push({
                name: 'zipFilter',
                fn: function (i, options) {
                    return i.name.toLowerCase().endsWith('.zip');
                }
            });

            uploader.onBeforeUploadItem = function (fileItem) {
                bladeNavigationService.setError(null, blade);
                uploadAbortedByWatchdog = false;
                activeUploadItem = fileItem;
                $scope.uploadFileName = fileItem._file && fileItem._file.name;
                $scope.uploadProgress = 0;
                $scope.uploadPhase = 'uploading';
                armUploadWatchdog();
            };

            uploader.onProgressItem = function (fileItem, progress) {
                $scope.uploadProgress = progress;
                // Once every byte is sent the library stops firing progress events; flip to
                // 'processing' so the UI stops implying completion while we wait on the server.
                $scope.uploadPhase = progress >= 100 ? 'processing' : 'uploading';
                // Reset the inactivity timer on each chunk: a slow-but-moving upload is fine;
                // only a true stall (or a server that never answers after 100 %) trips it.
                armUploadWatchdog();
            };

            uploader.onErrorItem = function (item, response, status, headers) {
                resetUploadState();
                setUploadError(describeUploadError(status, response));
            };

            uploader.onCancelItem = function (item, response, status, headers) {
                // Cancellation triggered by the watchdog already surfaced its own message; a
                // user-initiated cancel is intentionally silent. Just make sure state is clean.
                if (!uploadAbortedByWatchdog) {
                    resetUploadState();
                }
            };

            uploader.onSuccessItem = function (fileItem, asset, status, headers) {
                resetUploadState();
                // Use the relative blob url (e.g. "backups/<name>") so the backend resolves and reads
                // it from the configured blob store; the backend confines it to the backups folder.
                loadManifestForBackup(asset[0].relativeUrl, asset[0].name);
                // A fresh file just landed in storage — keep the existing-backups list in sync.
                loadExistingBackups();
            };
        }

        var commandStart = {
            name: "platform.blades.import-main.labels.start-import", icon: 'fa fa-download',
            executeMethod: () => $scope.startProcess(),
            canExecuteMethod: () => $scope.canStartProcess() && !blade.notification,
            target: 'import'
        };

        // Toolbar order: Back, Select all, Unselect all, Start restore. The start/cancel/close
        // command (target 'import') is swapped in place by switchCommandButton during the job,
        // so it must stay the single 'import'-targeted entry regardless of its position.
        blade.toolbarCommands = [
            {
                name: "platform.blades.import-main.labels.back", icon: 'fa fa-chevron-left',
                executeMethod: () => $scope.revertToFileSelection(),
                canExecuteMethod: () => $scope.importRequest.exportManifest && !blade.notification
            },
            {
                name: "platform.commands.select-all", icon: 'far fa-check-square',
                executeMethod: () => selectAll(true),
                canExecuteMethod: () => $scope.importRequest.exportManifest && !blade.notification
            },
            {
                name: "platform.commands.unselect-all", icon: 'far fa-square',
                executeMethod: () => selectAll(false),
                canExecuteMethod: () => $scope.importRequest.exportManifest && !blade.notification && $scope.canStartProcess()
            },
            commandStart
        ];

        var selectAll = function (action) {
            $scope.importRequest.handleSecurity = $scope.importRequest.exportManifest.handleSecurity && action;
            $scope.importRequest.handleBinaryData = $scope.importRequest.exportManifest.handleBinaryData && action;
            $scope.importRequest.handleSettings = $scope.importRequest.exportManifest.handleSettings && action;
            $scope.importRequest.handleDynamicProperties = $scope.importRequest.exportManifest.handleDynamicProperties && action;

            _.forEach($scope.importRequest.exportManifest.modules, (module) => module.isChecked = action);

            $scope.updateModuleSelection();
        }

        // Populate the "restore from a backup already in storage" list on open.
        loadExistingBackups();
    }]);
