using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Grpc.Core;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class PackageInstallationTests
{
    /// <summary>
    /// This test validates that the WithNpmPackageInstallation method creates
    /// installer resources with proper arguments and relationships.
    /// </summary>
    [Fact]
    public async Task WithNpmPackageInstallation_CanBeConfiguredWithInstallAndCIOptions()
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

        Assert.Equal("npm", installResource.Command);
        var args = await installResource.GetArgumentValuesAsync();
        Assert.Single(args);
        Assert.Equal("install", args[0]);

        Assert.Equal("npm", ciResource.Command);
        args = await ciResource.GetArgumentValuesAsync();
        Assert.Single(args);
        Assert.Equal("ci", args[0]);
    }

    [Fact]
    public void WithNpmPackageInstallation_ExcludedFromPublishMode()
    {
        var builder = DistributedApplication.CreateBuilder(["Publishing:Publisher=manifest", "Publishing:OutputPath=./publish"]);

        var nodeApp = builder.AddNpmApp("test-app", "./test-app");
        nodeApp.WithNpmPackageInstallation(useCI: false);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify the NodeApp resource exists
        var nodeResource = Assert.Single(appModel.Resources.OfType<NodeAppResource>());
        Assert.Equal("npm", nodeResource.Command);

        // Verify NO installer resource was created in publish mode
        var installerResources = appModel.Resources.OfType<NpmInstallerResource>().ToList();
        Assert.Empty(installerResources);

        // Verify no wait annotations were added
        Assert.False(nodeResource.TryGetAnnotationsOfType<WaitAnnotation>(out _));
    }
}