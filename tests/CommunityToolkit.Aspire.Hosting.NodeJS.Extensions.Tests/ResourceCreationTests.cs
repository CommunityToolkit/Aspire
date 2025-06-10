using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static Google.Protobuf.Reflection.GeneratedCodeInfo.Types;

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

    [Fact]
    public void WithNpmPackageInstallationDefaultsToInstallCommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddNpmApp("test-app", "./test-app");

        // Add package installation with default settings (should use npm install, not ci)
        nodeApp.WithNpmPackageInstallation(useCI: false);

        using var app = builder.Build();

        // Verify that the resource was created successfully
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NodeAppResource>());
        Assert.Equal("npm", resource.Command);
    }

    [Fact]
    public void WithNpmPackageInstallationCanUseCICommand()
    {
        var builder = DistributedApplication.CreateBuilder();

        var nodeApp = builder.AddNpmApp("test-app", "./test-app");

        // Add package installation with CI enabled
        nodeApp.WithNpmPackageInstallation(useCI: true);

        using var app = builder.Build();

        // Verify that the resource was created successfully
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<NodeAppResource>());
        Assert.Equal("npm", resource.Command);
    }

    [Fact]
    public void ViteAppConfiguresPortFromEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddViteApp("vite");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<NodeAppResource>());

        // Verify that command line arguments callback is configured
        Assert.True(resource.TryGetAnnotationsOfType<CommandLineArgsCallbackAnnotation>(out var argsCallbackAnnotations));
        List<object> args = [];
        var ctx = new CommandLineArgsCallbackContext(args);

        foreach (var annotation in argsCallbackAnnotations)
        {
            annotation.Callback(ctx);
        }

        Assert.Collection(args,
            arg => Assert.Equal("run", arg),
            arg => Assert.Equal("dev", arg),
            arg => Assert.Equal("--", arg),
            arg => Assert.Equal("--port", arg),
            arg => Assert.Equal(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "%PORT%" : "$PORT", arg)
        );
    }
}