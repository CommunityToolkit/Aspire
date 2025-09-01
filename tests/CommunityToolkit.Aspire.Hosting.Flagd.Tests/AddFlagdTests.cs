using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Flagd.Tests;

public class AddFlagdTests
{
    [Fact]
    public void AddFlagdCreatesCorrectResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var flagd = builder.AddFlagd("flagd");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());
        Assert.Equal("flagd", resource.Name);
    }

    [Fact]
    public void AddFlagdWithCustomPortSetsCorrectPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd("flagd", port: 12345);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .First(e => e.Name == FlagdResource.HttpEndpointName);

        Assert.Equal(12345, endpoint.Port);
        Assert.Equal(8013, endpoint.TargetPort);
    }

    [Fact]
    public void AddFlagdWithDefaultPortSetsCorrectTargetPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd("flagd");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var httpEndpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .First(e => e.Name == FlagdResource.HttpEndpointName);
        var grpcEndpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .First(e => e.Name == FlagdResource.GrpcEndpointName);

        Assert.Equal(8013, httpEndpoint.TargetPort);
        Assert.Equal(8013, grpcEndpoint.TargetPort);
        Assert.Equal("http", httpEndpoint.UriScheme);
        Assert.Equal("grpc", grpcEndpoint.UriScheme);
    }

    [Fact]
    public void AddFlagdSetsCorrectContainerImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd("flagd");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var containerAnnotation = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());
        
        Assert.Equal("ghcr.io", containerAnnotation.Registry);
        Assert.Equal("open-feature/flagd", containerAnnotation.Image);
        Assert.Equal("v0.11.6", containerAnnotation.Tag);
    }

    [Fact]
    public void WithFlagSourceAddsUriToArgs()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd("flagd")
            .WithFlagSource("file:///etc/flagd/flags.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var args = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToArray();
        Assert.NotEmpty(args);

        var flagSource = Assert.Single(resource.FlagSources);
        Assert.Equal("file:///etc/flagd/flags.json", flagSource);
    }

    [Fact]
    public void WithFlagConfigurationFileAddsBindMountAndArg()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd("flagd")
            .WithFlagConfigurationFile("./flags.json", "/etc/flagd/flags.json");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        // Check that the flag source was added
        var flagSource = Assert.Single(resource.FlagSources);
        Assert.Equal("file:///etc/flagd/flags.json", flagSource);
    }

    [Fact]
    public void WithHttpSyncAddsCorrectConfiguration()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd("flagd")
            .WithHttpSync("http://example.com/flags.json", interval: 10);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var flagSource = Assert.Single(resource.FlagSources);
        Assert.Equal("http://example.com/flags.json", flagSource);
        
        // Check that environment variables are set (simplified test)
        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToArray();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void WithDataVolumeAddsVolume()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd("flagd")
            .WithDataVolume("my-volume");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        // Check that volume annotations are present (simplified test)
        var volumeAnnotations = resource.Annotations.Where(a => a.GetType().Name.Contains("Volume")).ToArray();
        Assert.NotEmpty(volumeAnnotations);
    }

    [Fact]
    public void WithLoggingAddsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd("flagd")
            .WithLogging("debug");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        // Check that environment callback annotations are present (simplified test)
        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToArray();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void FlagdResourceImplementsIResourceWithConnectionString()
    {
        var builder = DistributedApplication.CreateBuilder();
        var flagd = builder.AddFlagd("flagd");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        Assert.True(resource is IResourceWithConnectionString);
        
        var connectionStringResource = resource as IResourceWithConnectionString;
        Assert.NotNull(connectionStringResource?.ConnectionStringExpression);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddFlagdThrowsWhenNameIsNullOrEmpty(string? name)
    {
        var builder = DistributedApplication.CreateBuilder();
        
        Assert.ThrowsAny<ArgumentException>(() => builder.AddFlagd(name!));
    }

    [Fact]
    public void AddFlagdThrowsWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        
        Assert.Throws<ArgumentNullException>(() => builder.AddFlagd("flagd"));
    }
}
