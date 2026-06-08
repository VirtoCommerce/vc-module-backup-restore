using FluentAssertions;
using VirtoCommerce.BackupRestore.Core;
using VirtoCommerce.BackupRestore.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Exceptions;
using Xunit;

namespace VirtoCommerce.BackupRestore.Tests;

[Trait("Category", "Unit")]
public class BackupBlobUrlTests
{
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\cmd.exe")]
    [InlineData("/etc/passwd")]
    [InlineData("subdir/file.zip")]
    [InlineData("backups/../secret.zip")]
    [InlineData("   ")]
    [InlineData(null)]
    public void GetSafe_MaliciousOrEmptyPath_ThrowsPlatformException(string relativePath)
    {
        // Act
        var act = () => BackupBlobUrl.GetSafe(relativePath);

        // Assert
        act.Should().Throw<PlatformException>();
    }

    [Fact]
    public void GetSafe_BareFileName_IsConfinedToBackupsFolder()
    {
        // Act
        var result = BackupBlobUrl.GetSafe("export.zip");

        // Assert
        result.Should().Be($"{ModuleConstants.BackupBlobFolder}/export.zip");
    }

    [Fact]
    public void GetSafe_RelativeUrlAlreadyRootedAtBackups_IsReturnedUnchanged()
    {
        // Arrange — the shape the asset upload returns as relativeUrl.
        var relativeUrl = $"{ModuleConstants.BackupBlobFolder}/my%20backup.zip";

        // Act
        var result = BackupBlobUrl.GetSafe(relativeUrl);

        // Assert
        result.Should().Be(relativeUrl);
    }
}
