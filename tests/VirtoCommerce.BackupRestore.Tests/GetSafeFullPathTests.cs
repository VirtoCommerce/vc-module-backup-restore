using System;
using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.BackupRestore.Core;
using VirtoCommerce.BackupRestore.Web.Controllers.Api;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Exceptions;
using VirtoCommerce.Platform.Core.PushNotifications;
using VirtoCommerce.Platform.Core.Security;
using Xunit;

namespace VirtoCommerce.BackupRestore.Tests;

[Trait("Category", "Unit")]
public class GetSafeFullPathTests
{
    private readonly BackupRestoreController _controller;

    public GetSafeFullPathTests()
    {
        var options = Options.Create(new PlatformOptions
        {
            DefaultExportFolder = Path.Combine(Path.GetTempPath(), "vc-test-exports"),
            LocalUploadFolderPath = Path.Combine(Path.GetTempPath(), "vc-test-uploads"),
        });

        _controller = new BackupRestoreController(
            Mock.Of<IBackupRestoreManager>(),
            Mock.Of<IPushNotificationManager>(),
            Mock.Of<IUserNameResolver>(),
            options,
            Mock.Of<IDataProtectionProvider>(),
            Mock.Of<ILogger<BackupRestoreController>>());
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\cmd.exe")]
    [InlineData("/etc/passwd")]
    [InlineData("subdir/file.zip")]
    [InlineData("   ")]
    public void DownloadExportFile_MaliciousPath_ThrowsPlatformException(string maliciousFileName)
    {
        // Act
        var act = () => _controller.DownloadExportFile(maliciousFileName);

        // Assert
        act.Should().Throw<PlatformException>();
    }

    [Fact]
    public void DownloadExportFile_ValidFileName_PassesPathValidation()
    {
        // Act
        var act = () => _controller.DownloadExportFile("export.zip");

        // Assert — GetSafeFullPath passes; the subsequent File.Open fails because the file doesn't exist.
        act.Should().Throw<Exception>().Which.Should().NotBeOfType<PlatformException>();
    }
}
