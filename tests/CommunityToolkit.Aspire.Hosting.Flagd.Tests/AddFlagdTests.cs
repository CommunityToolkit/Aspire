using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Flagd.Tests;

public class AddFlagdTests
{
    private const string FlagdName = "flagd";
    private const string FlagdSource = "./flags/flags.json";

    [Fact]
    public void AddFlagdCreatesCorrectResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var flagd = builder.AddFlagd(FlagdName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());
        Assert.Equal(FlagdName, resource.Name);
    }

    [Fact]
    public void AddFlagdWithCustomPortSetsCorrectPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName, port: 12345);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .First(e => e.Name == FlagdResource.HttpEndpointName);

        Assert.Equal(12345, endpoint.Port);
        Assert.Equal(8013, endpoint.TargetPort);
    }

    [Fact]
    public void AddFlagdWithCustomOfrepPortSetsCorrectPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName, ofrepPort: 54321);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .First(e => e.Name == FlagdResource.OfrepEndpointName);

        Assert.Equal(54321, endpoint.Port);
        Assert.Equal(8016, endpoint.TargetPort);
    }

    [Fact]
    public void AddFlagdSetsCorrectContainerImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var containerAnnotation = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal("ghcr.io", containerAnnotation.Registry);
        Assert.Equal("open-feature/flagd", containerAnnotation.Image);
        Assert.Equal("v0.12.9", containerAnnotation.Tag);
    }

    [Fact]
    public void AddFlagdSetsCorrectEndpoints()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var endpoints = resource.Annotations.OfType<EndpointAnnotation>().ToArray();

        Assert.Equal(3, endpoints.Length);

        var httpEndpoint = endpoints.First(e => e.Name == FlagdResource.HttpEndpointName);
        Assert.Equal(8013, httpEndpoint.TargetPort);
        Assert.Null(httpEndpoint.Port);

        var healthEndpoint = endpoints.First(e => e.Name == FlagdResource.HealthCheckEndpointName);
        Assert.Equal(8014, healthEndpoint.TargetPort);
        Assert.Null(healthEndpoint.Port);

        var ofrepEndpoint = endpoints.First(e => e.Name == FlagdResource.OfrepEndpointName);
        Assert.Equal(8016, ofrepEndpoint.TargetPort);
        Assert.Null(ofrepEndpoint.Port);
    }

    [Fact]
    public void AddFlagdAddsHealthCheck()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var healthCheckAnnotation = Assert.Single(resource.Annotations.OfType<HealthCheckAnnotation>());
        Assert.NotNull(healthCheckAnnotation);
    }

    [Fact]
    public void AddFlagdAddsStartArgument()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var commandLineArgs = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToArray();
        Assert.NotEmpty(commandLineArgs);
    }

    [Fact]
    public void WithLoggingAddsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName).WithLogging();

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToArray();
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void WithLoglevelDebugAddsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName).WithLoglevel(Microsoft.Extensions.Logging.LogLevel.Debug);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var envAnnotations = resource.Annotations.OfType<EnvironmentCallbackAnnotation>().ToArray();
        Assert.NotEmpty(envAnnotations);
    }

    [Theory]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Trace)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Information)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Warning)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Error)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.Critical)]
    [InlineData(Microsoft.Extensions.Logging.LogLevel.None)]
    public void WithLoglevelThrowsForUnsupportedLogLevels(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        var builder = DistributedApplication.CreateBuilder();
        var flagd = builder.AddFlagd(FlagdName);

        var exception = Assert.Throws<InvalidOperationException>(() => flagd.WithLoglevel(logLevel));
        Assert.Equal("Only debug log level is supported", exception.Message);
    }

    [Fact]
    public void WithLoglevelThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<FlagdResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithLoglevel(Microsoft.Extensions.Logging.LogLevel.Debug));
    }

    [Fact]
    public void WithBindFileSyncAddsBindMountAndArgs()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName).WithBindFileSync(FlagdSource);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var bindMountAnnotation = resource.Annotations.OfType<ContainerMountAnnotation>()
            .FirstOrDefault(m => m.Target == "/flags/");
        Assert.NotNull(bindMountAnnotation);
        // The source path may be converted to an absolute path, just check it's not null/empty
        Assert.NotNull(bindMountAnnotation.Source);
        Assert.NotEmpty(bindMountAnnotation.Source);

        var commandLineArgs = resource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().ToArray();
        Assert.NotEmpty(commandLineArgs);
    }

    [Fact]
    public void WithBindFileSyncWithCustomFilenameAddsCorrectArgs()
    {
        var builder = DistributedApplication.CreateBuilder();
        var customFilename = "custom-flags.json";
        builder.AddFlagd(FlagdName).WithBindFileSync(FlagdSource, customFilename);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var bindMountAnnotation = resource.Annotations.OfType<ContainerMountAnnotation>()
            .FirstOrDefault(m => m.Target == "/flags/");
        Assert.NotNull(bindMountAnnotation);
    }

    [Fact]
    public void FlagdResourceImplementsIResourceWithConnectionString()
    {
        var builder = DistributedApplication.CreateBuilder();
        var flagd = builder.AddFlagd(FlagdName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        Assert.True(resource is IResourceWithConnectionString);

        var connectionStringResource = resource as IResourceWithConnectionString;
        Assert.NotNull(connectionStringResource?.ConnectionStringExpression);
    }

    [Fact]
    public void FlagdResourceExposesEndpointReferences()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        Assert.NotNull(resource.PrimaryEndpoint);
        Assert.NotNull(resource.HealthCheckEndpoint);
        Assert.NotNull(resource.OfrepEndpoint);
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

        Assert.Throws<ArgumentNullException>(() => builder.AddFlagd(FlagdName));
    }

    [Fact]
    public void WithLoggingThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<FlagdResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithLogging());
    }

    [Fact]
    public void WithBindFileSyncThrowsWhenBuilderIsNull()
    {
        IResourceBuilder<FlagdResource> builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.WithBindFileSync(FlagdSource));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithBindFileSyncThrowsWhenFileSourceIsNullOrEmpty(string? fileSource)
    {
        var builder = DistributedApplication.CreateBuilder();
        var flagd = builder.AddFlagd(FlagdName);

        Assert.ThrowsAny<ArgumentException>(() => flagd.WithBindFileSync(fileSource!));
    }
}
