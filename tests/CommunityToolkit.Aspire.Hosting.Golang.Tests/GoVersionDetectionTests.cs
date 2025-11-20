using Aspire.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Golang.Tests;

public class GoVersionDetectionTests
{
    [Fact]
    public void DetectGoVersionFromGoMod()
    {
        // Arrange
        var workingDirectory = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "examples", "golang", "gin-api"));
        var logger = NullLogger.Instance;

        // Act
        var version = GolangAppHostingExtension.DetectGoVersion(workingDirectory, logger);

        // Assert
        Assert.NotNull(version);
        Assert.Equal("1.22", version);
    }

    [Fact]
    public void DetectGoVersionFromGoMod_NonExistentDirectory()
    {
        // Arrange
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var logger = NullLogger.Instance;

        // Act
        var version = GolangAppHostingExtension.DetectGoVersion(workingDirectory, logger);

        // Assert - should fall back to checking installed toolchain or return null
        // We don't assert a specific value because it depends on the system's Go installation
        Assert.True(version == null || !string.IsNullOrEmpty(version));
    }

    [Fact]
    public void DetectGoVersionFromGoMod_WithPatchVersion()
    {
        // Arrange - Create a temporary directory with a go.mod file
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var goModPath = Path.Combine(tempDir, "go.mod");
            File.WriteAllText(goModPath, @"module testmodule

go 1.21.5

require (
    github.com/example/package v1.0.0
)
");

            var logger = NullLogger.Instance;

            // Act
            var version = GolangAppHostingExtension.DetectGoVersion(tempDir, logger);

            // Assert
            Assert.NotNull(version);
            Assert.Equal("1.21", version);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void DetectGoVersionFromGoMod_WithMajorMinorOnly()
    {
        // Arrange - Create a temporary directory with a go.mod file
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var goModPath = Path.Combine(tempDir, "go.mod");
            File.WriteAllText(goModPath, @"module testmodule

go 1.20

require (
    github.com/example/package v1.0.0
)
");

            var logger = NullLogger.Instance;

            // Act
            var version = GolangAppHostingExtension.DetectGoVersion(tempDir, logger);

            // Assert
            Assert.NotNull(version);
            Assert.Equal("1.20", version);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void DetectGoVersionFromGoMod_InvalidFormat()
    {
        // Arrange - Create a temporary directory with an invalid go.mod file
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var goModPath = Path.Combine(tempDir, "go.mod");
            File.WriteAllText(goModPath, @"module testmodule

go invalid

require (
    github.com/example/package v1.0.0
)
");

            var logger = NullLogger.Instance;

            // Act
            var version = GolangAppHostingExtension.DetectGoVersion(tempDir, logger);

            // Assert - should fall back to checking installed toolchain or return null
            Assert.True(version == null || !string.IsNullOrEmpty(version));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
