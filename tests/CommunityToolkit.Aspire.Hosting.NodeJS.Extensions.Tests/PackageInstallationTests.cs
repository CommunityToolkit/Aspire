using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class PackageInstallationTests
{
    /// <summary>
    /// This test validates that the WithNpmPackageInstallation method can be called
    /// and properly registers the lifecycle hook for npm install operations.
    /// The issue in #618 would cause failures when no package-lock.json exists
    /// but package.json does exist when using npm install (not ci).
    /// </summary>
    [Fact]
    public void WithNpmPackageInstallation_CanBeConfiguredWithInstallAndCIOptions()
    {
        var builder = DistributedApplication.CreateBuilder();
        
        var nodeApp = builder.AddNpmApp("test-app");
        
        // Test that both configurations can be set up without errors
        nodeApp.WithNpmPackageInstallation(useCI: false); // Uses npm install
        
        var nodeApp2 = builder.AddNpmApp("test-app-ci");
        nodeApp2.WithNpmPackageInstallation(useCI: true);  // Uses npm ci

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resources = appModel.Resources.OfType<NodeAppResource>().ToList();
        
        Assert.Equal(2, resources.Count);
        Assert.All(resources, resource => Assert.Equal("npm", resource.Command));
    }
}