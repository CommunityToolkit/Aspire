using Aspire.Hosting;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void DefaultViteAppUsesNpm()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddViteApp("vite");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("npm", resource.Command);
    }

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

    [Fact]
    public void ViteAppUsesSpecifiedWorkingDirectory()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddViteApp("vite", "test");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal(Path.Combine(builder.AppHostDirectory, "test"), resource.WorkingDirectory);
    }

    [Theory]
    [InlineData("npm")]
    [InlineData("yarn")]
    [InlineData("pnpm")]
    public void ViteAppUsesSpecifiedPackageManager(string packageManager)
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddViteApp("vite", packageManager: packageManager);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal(packageManager, resource.Command);
    }

    [Fact]
    public void ViteAppHasExposedHttpEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddViteApp("vite");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints));

        Assert.Contains(endpoints, e => e.UriScheme == "http");
    }

    [Fact]
    public void ViteAppHasExposedHttpsEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddViteApp("vite", useHttps: true);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints));

        Assert.Contains(endpoints, e => e.UriScheme == "https");
    }


    [Fact]
    public void ViteAppHasExposedExternalHttpEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddViteApp("vite");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<NodeAppResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.True(resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints));

        Assert.Contains(endpoints, e => e.IsExternal);
    }
}