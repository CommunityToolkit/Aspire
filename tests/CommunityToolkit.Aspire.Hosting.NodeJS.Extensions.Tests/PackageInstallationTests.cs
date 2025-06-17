using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class PackageInstallationTests
{
    /// <summary>
    /// This test validates that the WithNpmPackageInstallation method creates
    /// installer resources with proper arguments and relationships.
    /// </summary>
    [Fact]
    public void WithNpmPackageInstallation_CanBeConfiguredWithInstallAndCIOptions()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddNpmApp("test-app", "./test-app");
        var nodeApp2 = builder.AddNpmApp("test-app-ci", "./test-app-ci");

        // Test that both configurations can be set up without errors
        nodeApp.WithNpmPackageInstallation(useCI: false); // Uses npm install
        nodeApp2.WithNpmPackageInstallation(useCI: true);  // Uses npm ci

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var nodeResources = appModel.Resources.OfType<NodeAppResource>().ToList();
        var installerResources = appModel.Resources.OfType<NpmInstallerResource>().ToList();

        Assert.Equal(2, nodeResources.Count);
        Assert.Equal(2, installerResources.Count);
        Assert.All(nodeResources, resource => Assert.Equal("npm", resource.Command));

        // Verify install vs ci commands
        var installResource = installerResources.Single(r => r.Name == "test-app-npm-install");
        var ciResource = installerResources.Single(r => r.Name == "test-app-ci-npm-install");
        
        Assert.Equal("install", installResource.InstallCommand);
        Assert.False(installResource.UseCI);
        
        Assert.Equal("ci", ciResource.InstallCommand);
        Assert.True(ciResource.UseCI);
    }
}