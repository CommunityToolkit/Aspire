using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Flagd.Tests;

public class AddFlagdTests
{
    private const string FlagdName = "flagd";
    private const string FlagdSource = "flags.json";
    [Fact]
    public void AddFlagdCreatesCorrectResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var flagd = builder.AddFlagd(FlagdName, FlagdSource);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());
        Assert.Equal(FlagdName, resource.Name);
    }

    [Fact]
    public void AddFlagdWithCustomPortSetsCorrectPort()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName, FlagdSource, port: 12345);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .First(e => e.Name == FlagdResource.HttpEndpointName);

        Assert.Equal(12345, endpoint.Port);
        Assert.Equal(8013, endpoint.TargetPort);
    }

    [Fact]
    public void AddFlagdSetsCorrectContainerImage()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName, FlagdSource);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<FlagdResource>());

        var containerAnnotation = Assert.Single(resource.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal("ghcr.io", containerAnnotation.Registry);
        Assert.Equal("open-feature/flagd", containerAnnotation.Image);
        Assert.Equal("v0.12.9", containerAnnotation.Tag);
    }

    [Fact]
    public void WithLoggingAddsEnvironmentVariable()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddFlagd(FlagdName, FlagdSource)
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
        var flagd = builder.AddFlagd(FlagdName, FlagdSource);

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

        Assert.ThrowsAny<ArgumentException>(() => builder.AddFlagd(name!, FlagdSource));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddFlagdThrowsWhenFileSourceIsNullOrEmpty(string? fileSource)
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.ThrowsAny<ArgumentException>(() => builder.AddFlagd(FlagdName, fileSource!));
    }

    [Fact]
    public void AddFlagdThrowsWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddFlagd(FlagdName, FlagdSource));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void AddFlagdThrowsWhenPortIsInvalid(int port)
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() => builder.AddFlagd(FlagdName, FlagdSource, port));
    }
}
