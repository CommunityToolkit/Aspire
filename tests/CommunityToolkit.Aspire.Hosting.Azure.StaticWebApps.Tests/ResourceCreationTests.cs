#pragma warning disable CTASPIRE003
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void TargetPort_Defaults_to_4280()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddSwaEmulator("swa");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SwaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("swa", resource.Name);

        var httpEndpoint = resource.GetEndpoint("http");
        Assert.Equal(4280, httpEndpoint.TargetPort);
    }

    [Fact]
    public void TargetPort_Can_Be_Overridden()
    {
        var builder = DistributedApplication.CreateBuilder();

        SwaResourceOptions options = new() { Port = 1234 };
        builder.AddSwaEmulator("swa", options);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SwaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("swa", resource.Name);

        var httpEndpoint = resource.GetEndpoint("http");
        Assert.Equal(options.Port, httpEndpoint.TargetPort);
    }

    [Fact]
    public void AppResource_Can_Be_Set()
    {
        var builder = DistributedApplication.CreateBuilder();

        var appResource = builder
                            .AddContainer("app", "test/container") // container image doesn't need to be valid as we aren't actually running it
                            .WithHttpEndpoint();

        builder.AddSwaEmulator("swa")
            .WithAppResource(appResource);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SwaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("swa", resource.Name);

        var result = resource.TryGetAnnotationsOfType<SwaAppEndpointAnnotation>(out var appResources);

        Assert.True(result);
        Assert.NotNull(appResources);
        Assert.Single(appResources);
    }

    [Fact]
    public void ApiResource_Can_Be_Set()
    {
        var builder = DistributedApplication.CreateBuilder();

        var apiResource = builder
                             .AddContainer("api", "test/container") // container image doesn't need to be valid as we aren't actually running it
                             .WithHttpEndpoint();

        builder.AddSwaEmulator("swa")
            .WithApiResource(apiResource);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SwaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("swa", resource.Name);

        var result = resource.TryGetAnnotationsOfType<SwaApiEndpointAnnotation>(out var apiResources);

        Assert.True(result);
        Assert.NotNull(apiResources);

        Assert.Single(apiResources);
    }

    [Fact]
    public void Start_Will_Be_An_Arg()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddSwaEmulator("swa");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SwaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("swa", resource.Name);

        var result = resource.TryGetAnnotationsOfType<CommandLineArgsCallbackAnnotation>(out var annotations);

        Assert.True(result);
        Assert.NotNull(annotations);

        Assert.Single(annotations);

        var annotation = annotations.Single();

        List<object> args = [];
        var ctx = new CommandLineArgsCallbackContext(args);

        annotation.Callback(ctx);

        Assert.Contains("start", args);
    }

    [Fact]
    public void Port_Will_Be_An_Arg()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddSwaEmulator("swa");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SwaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("swa", resource.Name);

        var result = resource.TryGetAnnotationsOfType<CommandLineArgsCallbackAnnotation>(out var annotations);

        Assert.True(result);
        Assert.NotNull(annotations);

        Assert.Single(annotations);

        var annotation = annotations.Single();

        List<object> args = [];
        var ctx = new CommandLineArgsCallbackContext(args);

        annotation.Callback(ctx);

        Assert.Contains("--port", args);
        Assert.Contains("4280", args);
    }

    [Fact]
    public void SwaResourceHasHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddSwaEmulator("swa");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SwaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("swa", resource.Name);

        var result = resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var annotations);

        Assert.True(result);
        Assert.NotNull(annotations);

        Assert.Single(annotations);
    }

    [Fact]
    public void AppResourceWillBeWaitedFor()
    {
        var builder = DistributedApplication.CreateBuilder();

        var appResource = builder
                            .AddContainer("app", "test/container") // container image doesn't need to be valid as we aren't actually running it
                            .WithHttpEndpoint();

        builder.AddSwaEmulator("swa")
            .WithAppResource(appResource);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SwaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("swa", resource.Name);

        var result = resource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations);

        Assert.True(result);
        Assert.NotNull(waitAnnotations);
        Assert.Single(waitAnnotations);

        var waitAnnotation = waitAnnotations.Single();
        Assert.Equal(appResource.Resource, waitAnnotation.Resource);
    }

    [Fact]
    public void ApiResourceWillBeWaitedFor()
    {
        var builder = DistributedApplication.CreateBuilder();

        var apiResource = builder
                            .AddContainer("api", "test/container") // container image doesn't need to be valid as we aren't actually running it
                            .WithHttpEndpoint();

        builder.AddSwaEmulator("swa")
            .WithApiResource(apiResource);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SwaResource>().SingleOrDefault();

        Assert.NotNull(resource);

        Assert.Equal("swa", resource.Name);

        var result = resource.TryGetAnnotationsOfType<WaitAnnotation>(out var waitAnnotations);

        Assert.True(result);
        Assert.NotNull(waitAnnotations);
        Assert.Single(waitAnnotations);

        var waitAnnotation = waitAnnotations.Single();
        Assert.Equal(apiResource.Resource, waitAnnotation.Resource);
    }
}
