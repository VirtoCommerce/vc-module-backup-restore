using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Server;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.BackupRestore.Core;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Exceptions;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.ExportImport.PushNotifications;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.PushNotifications;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using Permissions = VirtoCommerce.BackupRestore.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.BackupRestore.Web.Controllers.Api
{
    [Route("api/platform")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize]
    public class SampleDataController : Controller
    {
        private readonly IBackupRestoreManager _platformExportManager;
        private readonly IPushNotificationManager _pushNotifier;
        private readonly ISettingsManager _settingsManager;
        private readonly IUserNameResolver _userNameResolver;
        private readonly PlatformOptions _platformOptions;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IBlobStorageProvider _blobProvider;

        private static readonly object _lockObject = new();

        public SampleDataController(
            IBackupRestoreManager platformExportManager,
            IPushNotificationManager pushNotifier,
            ISettingsManager settingManager,
            IUserNameResolver userNameResolver,
            IOptions<PlatformOptions> options,
            IHttpClientFactory httpClientFactory,
            IBlobStorageProvider blobProvider)
        {
            _platformExportManager = platformExportManager;
            _pushNotifier = pushNotifier;
            _settingsManager = settingManager;
            _userNameResolver = userNameResolver;
            _httpClientFactory = httpClientFactory;
            _platformOptions = options.Value;
            _blobProvider = blobProvider;
        }

        [HttpGet]
        [Route("sampledata/discover")]
        [AllowAnonymous]
        public async Task<ActionResult<IList<SampleDataInfo>>> DiscoverSampleData()
        {
            return Ok(await InnerDiscoverSampleDataAsync());
        }

        [HttpPost]
        [Route("sampledata/autoinstall")]
        [Authorize(Permissions.Restore)]
        public async Task<ActionResult<SampleDataImportPushNotification>> TryToAutoInstallSampleData()
        {
            var sampleData = (await InnerDiscoverSampleDataAsync()).FirstOrDefault(x => !x.Url.IsNullOrEmpty());
            if (sampleData != null)
            {
                return Ok(StartImportSampleData(sampleData.Name));
            }

            return Ok();
        }

        [HttpPost]
        [Route("sampledata/import")]
        [Authorize(Permissions.Restore)]
        public async Task<ActionResult<SampleDataImportPushNotification>> ImportSampleData([FromQuery] string name = null, [FromQuery] string url = null)
        {
            var sampleDataList = await InnerDiscoverSampleDataAsync();

            SampleDataInfo sampleData = null;

            if (!string.IsNullOrEmpty(name))
            {
                sampleData = sampleDataList.FirstOrDefault(x => x.Name == name);
            }
            else if (!string.IsNullOrEmpty(url))
            {
                sampleData = sampleDataList.FirstOrDefault(x => x.Url == url);
            }

            if (sampleData != null)
            {
                return Ok(StartImportSampleData(sampleData.Name));
            }

            return Ok();
        }

        private SampleDataImportPushNotification StartImportSampleData(string name)
        {
            SampleDataImportPushNotification pushNotification = null;

            lock (_lockObject)
            {
                var sampleDataState = EnumUtility.SafeParse(_settingsManager.GetValue<string>(PlatformConstants.Settings.Setup.SampleDataState), SampleDataState.Undefined);
                if (sampleDataState == SampleDataState.Undefined)
                {
                    _settingsManager.SetValue(PlatformConstants.Settings.Setup.SampleDataState.Name, SampleDataState.Processing);

                    pushNotification = new SampleDataImportPushNotification(User.Identity?.Name);
                    pushNotification.Title = "Sample data import process";

                    _pushNotifier.Send(pushNotification);
                    var jobId = BackgroundJob.Enqueue(() => SampleDataImportBackgroundAsync(name, pushNotification, null, CancellationToken.None));
                    pushNotification.JobId = jobId;
                }
            }

            return pushNotification;
        }

        /// <summary>
        /// This method used for azure automatically deployment
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("sampledata/state")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [AllowAnonymous]
        public ActionResult<SampleDataState> GetSampleDataState()
        {
            var state = EnumUtility.SafeParse(_settingsManager.GetValue<string>(PlatformConstants.Settings.Setup.SampleDataState), SampleDataState.Undefined);
            return Ok(state);
        }

        [HttpGet]
        [Route("export/manifest/new")]
        [Authorize(Permissions.Backup)]
        public ActionResult<PlatformExportManifest> GetNewExportManifest()
        {
            return Ok(_platformExportManager.GetNewExportManifest(_userNameResolver.GetCurrentUserName()));
        }

        [HttpGet]
        [Route("export/manifest/load")]
        [Authorize(Permissions.Restore)]
        public async Task<ActionResult<PlatformExportManifest>> LoadExportManifest([FromQuery] string fileUrl)
        {
            if (string.IsNullOrEmpty(fileUrl))
            {
                throw new ArgumentNullException(nameof(fileUrl));
            }

            var blobUrl = BackupBlobUrl.GetSafe(fileUrl);

            PlatformExportManifest retVal;
            await using (var stream = await _blobProvider.OpenReadAsync(blobUrl))
            {
                retVal = _platformExportManager.ReadExportManifest(stream);
            }
            return Ok(retVal);
        }

        private async Task<IList<SampleDataInfo>> InnerDiscoverSampleDataAsync()
        {
            var sampleDataUrl = _platformOptions.SampleDataUrl;
            if (string.IsNullOrEmpty(sampleDataUrl))
            {
                return [];
            }

            //Direct file mode
            if (sampleDataUrl.EndsWith(".zip"))
            {
                return new List<SampleDataInfo>
                {
                    new()
                    {
                        Name = Path.GetFileNameWithoutExtension(sampleDataUrl),
                        Url = sampleDataUrl,
                    },
                };
            }

            //Discovery mode
            var manifestUrl = sampleDataUrl + "/manifest.json";
            var httpClient = _httpClientFactory.CreateClient();
            await using var stream = await httpClient.GetStreamAsync(new Uri(manifestUrl));
            //Add empty template
            var result = new List<SampleDataInfo>
            {
                new() { Name = "Empty" }
            };

            //Need filter unsupported versions and take one most new sample data
            var sampleDataInfos = stream.DeserializeJson<List<SampleDataInfo>>()
                .Select(x => new
                {
                    Version = SemanticVersion.Parse(x.PlatformVersion),
                    x.Name,
                    Data = x
                })
                .Where(x => x.Version.IsCompatibleWith(PlatformVersion.CurrentVersion))
                .GroupBy(x => x.Name)
                .Select(x => x.OrderByDescending(y => y.Version).First().Data)
                .ToList();

            //Convert relative  sample data urls to absolute
            foreach (var sampleDataInfo in sampleDataInfos)
            {
                if (!Uri.IsWellFormedUriString(sampleDataInfo.Url, UriKind.Absolute))
                {
                    var uri = new Uri(sampleDataUrl);
                    sampleDataInfo.Url = new Uri(uri, uri.AbsolutePath + "/" + sampleDataInfo.Url).ToString();
                }
            }

            result.AddRange(sampleDataInfos);

            return result;
        }

        public async Task SampleDataImportBackgroundAsync(string name, SampleDataImportPushNotification pushNotification, PerformContext context, CancellationToken cancellationToken)
        {
            void progressCallback(ExportImportProgressInfo x)
            {
                pushNotification.Patch(x);
                pushNotification.JobId = context.BackgroundJob.Id;
                _pushNotifier.Send(pushNotification);
            }

            try
            {
                var url = (await InnerDiscoverSampleDataAsync()).FirstOrDefault(x => x.Name == name)?.Url;
                if (url is null)
                {
                    return;
                }

                pushNotification.Description = "Start downloading from " + url;

                await _pushNotifier.SendAsync(pushNotification);

                // Stage the downloaded sample data in shared blob storage (Assets module) instead of a
                // local temp file, so the module performs no local file I/O.
                var blobUrl = BackupBlobUrl.GetSafe(Path.GetFileName(url));

                await using (var blobStream = await _blobProvider.OpenWriteAsync(blobUrl))
                {
                    await DownloadFileAsync(new Uri(url), blobStream, async (bytesReceived, bytesTotal) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var message = $"Sample data {bytesReceived.ToHumanReadableSize()} of {bytesTotal.ToHumanReadableSize()} downloading...";
                        if (message != pushNotification.Description)
                        {
                            pushNotification.Description = message;
                            await _pushNotifier.SendAsync(pushNotification);
                        }
                    });
                }

                await using (var stream = await _blobProvider.OpenReadAsync(blobUrl))
                {
                    var manifest = _platformExportManager.ReadExportManifest(stream);
                    if (manifest != null)
                    {
                        await _platformExportManager.ImportAsync(stream, manifest, progressCallback, cancellationToken);
                    }
                }

                // The staged sample data is consumed; remove it so it doesn't linger in blob storage.
                await _blobProvider.RemoveAsync([blobUrl]);
            }
            catch (JobAbortedException)
            {
                //do nothing
            }
            catch (Exception ex)
            {
                var message = ex.ExpandExceptionMessage();
                pushNotification.Errors.Add(message);
                pushNotification.ProgressLog ??= new List<ProgressMessage>();
                pushNotification.ProgressLog.Add(new ProgressMessage { Level = ProgressMessageLevel.Error, Message = message });
            }
            finally
            {
                await _settingsManager.SetValueAsync(PlatformConstants.Settings.Setup.SampleDataState.Name, SampleDataState.Completed);
                pushNotification.Description = pushNotification.Errors.Count > 0
                    ? "Sample data import process completed with errors."
                    : "Sample data import process completed successfully.";
                pushNotification.Finished = DateTime.UtcNow;
                await _pushNotifier.SendAsync(pushNotification);
            }
        }


        private async Task DownloadFileAsync(Uri uri, Stream writeStream, Func<long, long, Task> progress)
        {
            var httpClient = _httpClientFactory.CreateClient();

            var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength ?? -1L;
            var bytesReceived = 0L;
            var bytesTotal = contentLength < 0 ? 0L : contentLength;

            const int defaultBufferSize = 65536;
            var bufferSize = contentLength < 0 || contentLength > defaultBufferSize ? defaultBufferSize : (int)contentLength;
            var buffer = new byte[bufferSize];

            await using var readStream = await response.Content.ReadAsStreamAsync();

            while (true)
            {
                var bytesRead = await readStream.ReadAsync(new Memory<byte>(buffer)).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                bytesReceived += bytesRead;

                if (contentLength < 0)
                {
                    bytesTotal = bytesReceived;
                }

                await writeStream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead)).ConfigureAwait(false);

                await progress(bytesReceived, bytesTotal);
            }
        }
    }
}
