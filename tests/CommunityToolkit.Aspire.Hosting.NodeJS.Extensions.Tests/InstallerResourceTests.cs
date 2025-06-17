using Aspire.Hosting.ApplicationModel;
using System.Runtime.InteropServices;
using Xunit;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class InstallerResourceTests
{
    [Fact]
    public void NpmInstallerResource_DefaultsToInstallCommand()
    {
        var installer = new NpmInstallerResource("test-installer", "/test/path");
        
        Assert.Equal("test-installer", installer.Name);
        Assert.Equal("/test/path", installer.WorkingDirectory);
        Assert.Equal("install", installer.InstallCommand);
        Assert.False(installer.UseCI);
        Assert.Equal("package-lock.json", installer.LockfileName);
    }

    [Fact]
    public void NpmInstallerResource_CanUseCICommand()
    {
        var installer = new NpmInstallerResource("test-installer", "/test/path", useCI: true);
        
        Assert.Equal("ci", installer.InstallCommand);
        Assert.True(installer.UseCI);
    }

    [Fact]
    public void NpmInstallerResource_GeneratesCorrectArguments()
    {
        var installer = new NpmInstallerResource("test-installer", "/test/path");
        var args = installer.GetArguments();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(["/c", "npm", "install"], args);
        }
        else
        {
            Assert.Equal(["install"], args);
        }
    }

    [Fact]
    public void YarnInstallerResource_HasCorrectProperties()
    {
        var installer = new YarnInstallerResource("test-installer", "/test/path");
        
        Assert.Equal("test-installer", installer.Name);
        Assert.Equal("/test/path", installer.WorkingDirectory);
        Assert.Equal("install", installer.InstallCommand);
        Assert.Equal("yarn.lock", installer.LockfileName);
    }

    [Fact]
    public void YarnInstallerResource_GeneratesCorrectArguments()
    {
        var installer = new YarnInstallerResource("test-installer", "/test/path");
        var args = installer.GetArguments();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(["/c", "yarn", "install"], args);
        }
        else
        {
            Assert.Equal(["install"], args);
        }
    }

    [Fact]
    public void PnpmInstallerResource_HasCorrectProperties()
    {
        var installer = new PnpmInstallerResource("test-installer", "/test/path");
        
        Assert.Equal("test-installer", installer.Name);
        Assert.Equal("/test/path", installer.WorkingDirectory);
        Assert.Equal("install", installer.InstallCommand);
        Assert.Equal("pnpm-lock.yaml", installer.LockfileName);
    }

    [Fact]
    public void PnpmInstallerResource_GeneratesCorrectArguments()
    {
        var installer = new PnpmInstallerResource("test-installer", "/test/path");
        var args = installer.GetArguments();
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(["/c", "pnpm", "install"], args);
        }
        else
        {
            Assert.Equal(["install"], args);
        }
    }
}