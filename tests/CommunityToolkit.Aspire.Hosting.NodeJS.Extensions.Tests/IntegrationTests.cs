using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

/// <summary>
/// Integration test that demonstrates the new resource-based package installer architecture.
/// This shows how installer resources appear as separate resources in the application model.
/// </summary>
public class IntegrationTests
{
    [Fact]
    public void ResourceBasedPackageInstallersAppearInApplicationModel()
    {
        var builder = DistributedApplication.CreateBuilder();

        // Add multiple Node.js apps with different package managers
        var viteApp = builder.AddViteApp("vite-app", "./frontend")
            .WithNpmPackageInstallation(useCI: true);

        var yarnApp = builder.AddYarnApp("yarn-app", "./backend")
            .WithYarnPackageInstallation();

        var pnpmApp = builder.AddPnpmApp("pnpm-app", "./admin")
            .WithPnpmPackageInstallation();

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify all Node.js app resources are present
        var nodeResources = appModel.Resources.OfType<NodeAppResource>().ToList();
        Assert.Equal(3, nodeResources.Count);

        // Verify all installer resources are present as separate resources
        var npmInstallers = appModel.Resources.OfType<NpmInstallerResource>().ToList();
        var yarnInstallers = appModel.Resources.OfType<YarnInstallerResource>().ToList();
        var pnpmInstallers = appModel.Resources.OfType<PnpmInstallerResource>().ToList();

        Assert.Single(npmInstallers);
        Assert.Single(yarnInstallers);
        Assert.Single(pnpmInstallers);

        // Verify installer resources have expected names (would appear on dashboard)
        Assert.Equal("vite-app-npm-install", npmInstallers[0].Name);
        Assert.Equal("yarn-app-yarn-install", yarnInstallers[0].Name);
        Assert.Equal("pnpm-app-pnpm-install", pnpmInstallers[0].Name);

        // Verify parent-child relationships
        foreach (var installer in npmInstallers.Cast<IResource>()
            .Concat(yarnInstallers.Cast<IResource>())
            .Concat(pnpmInstallers.Cast<IResource>()))
        {
            Assert.True(installer.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var relationships));
            Assert.Single(relationships);
            Assert.Equal("Parent", relationships.First().Relationship);
        }

        // Verify all Node.js apps wait for their installers
        foreach (var nodeApp in nodeResources)
        {
            Assert.True(nodeApp.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
            Assert.Single(waitAnnotations);
            
            var waitedResource = waitAnnotations.First().Resource;
            Assert.True(waitedResource is NpmInstallerResource ||
                       waitedResource is YarnInstallerResource ||
                       waitedResource is PnpmInstallerResource);
        }
    }

    [Fact]
    public void InstallerResourcesHaveCorrectExecutableConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddNpmApp("test-app", "./test")
            .WithNpmPackageInstallation(useCI: true);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var installer = Assert.Single(appModel.Resources.OfType<NpmInstallerResource>());

        // Verify it's configured as an ExecutableResource
        Assert.IsAssignableFrom<ExecutableResource>(installer);
        
        // Verify working directory matches parent
        var parentApp = Assert.Single(appModel.Resources.OfType<NodeAppResource>());
        Assert.Equal(parentApp.WorkingDirectory, installer.WorkingDirectory);

        // Verify command arguments are configured
        Assert.True(installer.TryGetAnnotationsOfType<CommandLineArgsCallbackAnnotation>(out var argsAnnotations) ||
                   installer.TryGetAnnotationsOfType<CommandLineArgsAnnotation>(out var directArgs));
    }
}