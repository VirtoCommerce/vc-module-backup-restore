using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VirtoCommerce.BackupRestore.Core;
using VirtoCommerce.BackupRestore.Data;
using VirtoCommerce.Platform.Core.DynamicProperties;
using VirtoCommerce.Platform.Core.ExportImport;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.Platform.Core.Settings;
using Xunit;

namespace VirtoCommerce.BackupRestore.Tests;

[Trait("Category", "Unit")]
public class BackupRestoreManagerBinaryDataTests
{
    private const string ModuleId = "VirtoCommerce.Test";
    private const string ModuleEntryName = ModuleId + ".json";
    private const string BinaryDataReference = "assets/catalog/products/image.jpg";

    private static readonly byte[] ModuleJson = Encoding.UTF8.GetBytes("{\"products\":[{\"id\":\"product\"}]}");
    private static readonly byte[] BinaryData = [1, 3, 5, 7, 9];

    [Fact]
    public async Task ExportAndImportAsync_BinaryDataModule_UsesReadableModuleEntryAndTopLevelAssets()
    {
        // Arrange
        var module = new TestBinaryDataModule();
        var manager = CreateManager(module);
        var manifest = CreateManifest(handleBinaryData: true);
        ExportImportProgressInfo exportProgress = null;
        ExportImportProgressInfo importProgress = null;
        using var backup = new MemoryStream();

        // Act
        await manager.ExportAsync(backup, manifest, x => exportProgress = x, CancellationToken.None);

        // Assert export package layout before exercising import.
        module.BinaryExportCalls.Should().Be(1);
        module.LegacyExportCalls.Should().Be(0);
        exportProgress.Errors.Should().BeEmpty();
        manifest.Modules.Single().PartUri.Should().Be(ModuleEntryName);

        using (var archive = new ZipArchive(new MemoryStream(backup.ToArray()), ZipArchiveMode.Read))
        {
            archive.Entries.Select(x => x.FullName).Should().Contain([
                ModuleEntryName,
                BinaryDataReference,
                "Manifest.json",
            ]);
            (await ReadEntryAsync(archive, ModuleEntryName)).Should().Equal(ModuleJson);
            (await ReadEntryAsync(archive, BinaryDataReference)).Should().Equal(BinaryData);
        }

        backup.Position = 0;
        await manager.ImportAsync(backup, manifest, x => importProgress = x, CancellationToken.None);

        module.BinaryImportCalls.Should().Be(1);
        module.LegacyImportCalls.Should().Be(0);
        importProgress.Errors.Should().BeEmpty();
        module.ImportedModuleJson.Should().Equal(ModuleJson);
        module.ImportedBinaryData.Should().Equal(BinaryData);
    }

    [Fact]
    public async Task ExportAsync_WithoutBinaryData_UsesLegacySingleStreamContract()
    {
        // Arrange
        var module = new TestBinaryDataModule();
        var manager = CreateManager(module);
        var manifest = CreateManifest(handleBinaryData: false);
        ExportImportProgressInfo progress = null;
        using var backup = new MemoryStream();

        // Act
        await manager.ExportAsync(backup, manifest, x => progress = x, CancellationToken.None);

        // Assert
        module.LegacyExportCalls.Should().Be(1);
        module.BinaryExportCalls.Should().Be(0);
        progress.Errors.Should().BeEmpty();

        using var archive = new ZipArchive(new MemoryStream(backup.ToArray()), ZipArchiveMode.Read);
        (await ReadEntryAsync(archive, ModuleEntryName)).Should().Equal(ModuleJson);
        archive.GetEntry(BinaryDataReference).Should().BeNull();
    }

    private static BackupRestoreManager CreateManager(TestBinaryDataModule module)
    {
        var moduleInfo = new ManifestModuleInfo().LoadFromManifest(new ModuleManifest
        {
            Id = ModuleId,
            Version = "1.0.0",
            PlatformVersion = "3.1059.0",
        });
        moduleInfo.ModuleInstance = module;
        module.ModuleInfo = moduleInfo;

        var moduleService = new Mock<IModuleService>();
        moduleService.Setup(x => x.GetInstalledModules()).Returns([moduleInfo]);

        var userManager = new UserManager<ApplicationUser>(
            Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        var roleManager = new RoleManager<Role>(
            Mock.Of<IRoleStore<Role>>(), null, null, null, null);

        return new BackupRestoreManager(
            userManager,
            roleManager,
            Mock.Of<ISettingsManager>(),
            Mock.Of<IDynamicPropertyService>(),
            Mock.Of<IDynamicPropertySearchService>(),
            moduleService.Object,
            Mock.Of<IDynamicPropertyDictionaryItemsService>(),
            Mock.Of<IDynamicPropertyDictionaryItemsSearchService>(),
            Mock.Of<IUserApiKeyService>(),
            Mock.Of<IUserApiKeySearchService>(),
            new SharpZipBackupArchiveFactory());
    }

    private static PlatformExportManifest CreateManifest(bool handleBinaryData)
    {
        return new PlatformExportManifest
        {
            HandleBinaryData = handleBinaryData,
            Modules =
            [
                new ExportModuleInfo
                {
                    Id = ModuleId,
                    Version = "1.0.0",
                },
            ],
        };
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        entry.Should().NotBeNull();
        await using var entryStream = entry.Open();
        using var output = new MemoryStream();
        await entryStream.CopyToAsync(output);
        return output.ToArray();
    }

    private sealed class TestBinaryDataModule : IModule, IExportBinaryDataSupport, IImportBinaryDataSupport
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public int LegacyExportCalls { get; private set; }
        public int BinaryExportCalls { get; private set; }
        public int LegacyImportCalls { get; private set; }
        public int BinaryImportCalls { get; private set; }
        public byte[] ImportedModuleJson { get; private set; }
        public byte[] ImportedBinaryData { get; private set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
        }

        public void Uninstall()
        {
        }

        public async Task ExportAsync(
            Stream outStream,
            ExportImportOptions options,
            Action<ExportImportProgressInfo> progressCallback,
            CancellationToken cancellationToken)
        {
            LegacyExportCalls++;
            await outStream.WriteAsync(ModuleJson, cancellationToken);
        }

        public async Task ExportAsync(
            Stream outStream,
            IExportBinaryDataWriter binaryDataWriter,
            ExportImportOptions options,
            Action<ExportImportProgressInfo> progressCallback,
            CancellationToken cancellationToken)
        {
            BinaryExportCalls++;
            await outStream.WriteAsync(ModuleJson, cancellationToken);
            await using var binaryDataStream = new MemoryStream(BinaryData, writable: false);
            await binaryDataWriter.WriteAsync(
                BinaryDataReference,
                binaryDataStream,
                cancellationToken);
        }

        public Task ImportAsync(
            Stream inputStream,
            ExportImportOptions options,
            Action<ExportImportProgressInfo> progressCallback,
            CancellationToken cancellationToken)
        {
            LegacyImportCalls++;
            return Task.CompletedTask;
        }

        public async Task ImportAsync(
            Stream inputStream,
            IImportBinaryDataReader binaryDataReader,
            ExportImportOptions options,
            Action<ExportImportProgressInfo> progressCallback,
            CancellationToken cancellationToken)
        {
            BinaryImportCalls++;
            ImportedModuleJson = await ReadAllBytesAsync(inputStream, cancellationToken);

            await using var binaryDataStream = await binaryDataReader.OpenReadAsync(BinaryDataReference, cancellationToken);
            ImportedBinaryData = await ReadAllBytesAsync(binaryDataStream, cancellationToken);
        }

        private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var output = new MemoryStream();
            await stream.CopyToAsync(output, cancellationToken);
            return output.ToArray();
        }
    }
}
