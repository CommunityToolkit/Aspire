using System.Net.Sockets;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Hosting.Chroma;
using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Chroma.Tests;

public class AddChromaTests
{
    [Fact]
    public void AddChromaContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddChroma("chroma");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<ChromaResource>());
        Assert.Equal("chroma", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(8000, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(ChromaContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(ChromaContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(ChromaContainerImageTags.Registry, containerAnnotation.Registry);
    }

    [Fact]
    public async Task ChromaCreatesConnectionString()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder
            .AddChroma("chroma")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27020));

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<ChromaResource>()) as IResourceWithConnectionString;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        Assert.Equal($"Endpoint=http://localhost:27020", connectionString);
        Assert.Equal("Endpoint=http://{chroma.bindings.http.host}:{chroma.bindings.http.port}", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public async Task WithDataVolumeAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddChroma("chroma")
            .WithDataVolume("chroma-data");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<ChromaResource>());
        var mountAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal("chroma-data", mountAnnotation.Source);
        Assert.Equal("/data", mountAnnotation.Target);
        Assert.Equal(ContainerMountType.Volume, mountAnnotation.Type);

        var environment = await containerResource.GetEnvironmentVariablesAsync();
        Assert.Equal("/data", environment["PERSIST_DIRECTORY"]);
    }

    [Fact]
    public async Task WithDataBindMountAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddChroma("chroma")
            .WithDataBindMount("./chroma-data");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<ChromaResource>());
        var mountAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.NotNull(mountAnnotation.Source);
        Assert.Equal(Path.GetFullPath("./chroma-data").Replace('\\', '/'), mountAnnotation.Source.Replace('\\', '/'));
        Assert.Equal("/data", mountAnnotation.Target);
        Assert.Equal(ContainerMountType.BindMount, mountAnnotation.Type);

        var environment = await containerResource.GetEnvironmentVariablesAsync();
        Assert.Equal("/data", environment["PERSIST_DIRECTORY"]);
    }

    // While the AppHost runs, Chroma 1.x writes the requests it serves to its log, which the dashboard shows. A published app does not get the variable.
    [Theory]
    [InlineData(DistributedApplicationOperation.Run, true)]
    [InlineData(DistributedApplicationOperation.Publish, false)]
    public async Task ChromaLogsItsRequestsOnlyWhileTheAppHostRuns(DistributedApplicationOperation operation, bool logsRequests)
    {
        using var builder = TestDistributedApplicationBuilder.Create(operation);
        var chroma = builder.AddChroma("chroma");

        var environment = await chroma.Resource.GetEnvironmentVariablesAsync(operation);

        Assert.Equal(logsRequests, environment.TryGetValue("CHROMA_STDOUT_TRACING", out var value) && value == "true");
    }

    [Fact]
    public async Task ChromaRequestLogsCanBeTurnedOff()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);
        var chroma = builder.AddChroma("chroma").WithEnvironment("CHROMA_STDOUT_TRACING", "false");

        var environment = await chroma.Resource.GetEnvironmentVariablesAsync();

        Assert.Equal("false", environment["CHROMA_STDOUT_TRACING"]);
    }

    [Fact]
    public void AddChromaWithAPortBindsThatHostPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();
        appBuilder.AddChroma("chroma", port: 8123);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<ChromaResource>());
        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal(8123, endpoint.Port);
        Assert.Equal(8000, endpoint.TargetPort);
    }

    // The documentation of AddChroma writes the default image and tag as text, which the TypeScript AppHost shows too;
    // this test fails when they no longer match the image of the resource.
    [Fact]
    public void AddChromaDocumentationNamesTheDefaultImageAndTag()
    {
        var documentationPath = Path.ChangeExtension(typeof(ChromaResource).Assembly.Location, ".xml");
        var documentation = System.Xml.Linq.XDocument.Load(documentationPath);

        var summary = documentation.Descendants("member")
            .Single(member => ((string?)member.Attribute("name"))?.StartsWith("M:Aspire.Hosting.ChromaBuilderExtensions.AddChroma(", StringComparison.Ordinal) == true)
            .Element("summary")!.Value;

        Assert.Contains($"The default image is \"{ChromaContainerImageTags.Image}\" and the tag is \"{ChromaContainerImageTags.Tag}\".", summary);
    }
}
