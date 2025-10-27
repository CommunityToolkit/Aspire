using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Runtime.InteropServices;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void YarnAppUsesYarnCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddYarnApp("yarn", Environment.CurrentDirectory);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("yarn", resource.Command);
    }

    [Fact]
    public void PnpmAppUsesPnpmCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPnpmApp("pnpm", Environment.CurrentDirectory);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("pnpm", resource.Command);
    }

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("pnpm", resource.Command);
    }

    [Fact]
    public async Task WithYarnPackageInstallationCreatesInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddYarnApp("test-app", "./test-app");
        nodeApp.WithYarnPackageInstallation();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify the NodeApp resource exists
        var nodeResource = Assert.Single(appModel.Resources.OfType<NodeAppResource>());
        Assert.Equal("yarn", nodeResource.Command);

        // Verify the installer resource was created
        var installerResource = Assert.Single(appModel.Resources.OfType<YarnInstallerResource>());
        Assert.Equal("test-app-yarn-install", installerResource.Name);
        Assert.Equal("yarn", installerResource.Command);
        var args = await installerResource.GetArgumentValuesAsync();
        Assert.Single(args);
        Assert.Equal("install", args[0]);

        // Verify the parent-child relationship
        Assert.True(installerResource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var relationships));
        var relationship = Assert.Single(relationships);
        Assert.Same(nodeResource, relationship.Resource);
        Assert.Equal("Parent", relationship.Type);

        // Verify the wait annotation on the parent
        Assert.True(nodeResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        var waitAnnotation = Assert.Single(waitAnnotations);
        Assert.Same(installerResource, waitAnnotation.Resource);
    }

    [Fact]
    public async Task WithPnpmPackageInstallationCreatesInstallerResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddPnpmApp("test-app", "./test-app");
        nodeApp.WithPnpmPackageInstallation();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify the NodeApp resource exists
        var nodeResource = Assert.Single(appModel.Resources.OfType<NodeAppResource>());
        Assert.Equal("pnpm", nodeResource.Command);

        // Verify the installer resource was created
        var installerResource = Assert.Single(appModel.Resources.OfType<PnpmInstallerResource>());
        Assert.Equal("test-app-pnpm-install", installerResource.Name);
        Assert.Equal("pnpm", installerResource.Command);
        var args = await installerResource.GetArgumentValuesAsync();
        Assert.Single(args);
        Assert.Equal("install", args[0]);

        // Verify the parent-child relationship
        Assert.True(installerResource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var relationships));
        var relationship = Assert.Single(relationships);
        Assert.Same(nodeResource, relationship.Resource);
        Assert.Equal("Parent", relationship.Type);

        // Verify the wait annotation on the parent
        Assert.True(nodeResource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations));
        var waitAnnotation = Assert.Single(waitAnnotations);
        Assert.Same(installerResource, waitAnnotation.Resource);
    }
}