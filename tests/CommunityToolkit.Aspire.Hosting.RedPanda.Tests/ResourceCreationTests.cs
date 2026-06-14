using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.RedPanda.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void AddRedPandaShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddRedPanda("redpanda"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddRedPandaShouldThrowWhenNameIsNullOrEmpty(string? name)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        Assert.ThrowsAny<ArgumentException>(() => builder.AddRedPanda(name!));
    }

    [Fact]
    public void AddRedPandaSetsContainerImageAnnotations()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddRedPanda("redpanda");

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        RedPandaServerResource resource = Assert.Single(appModel.Resources.OfType<RedPandaServerResource>());
        Assert.Equal("redpanda", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? image));
        Assert.Equal("redpandadata/redpanda", image.Image);
        Assert.Equal("v26.1.10", image.Tag);
        Assert.Equal("docker.redpanda.com", image.Registry);
    }

    [Fact]
    public void AddRedPandaCreatesExpectedEndpoints()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddRedPanda("redpanda");

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        RedPandaServerResource resource = Assert.Single(appModel.Resources.OfType<RedPandaServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<EndpointAnnotation>? annotations));
        Dictionary<string, EndpointAnnotation> endpoints = annotations!.ToDictionary(e => e.Name);

        Assert.Equal(4, endpoints.Count);
        Assert.Equal(RedPandaServerResource.KafkaBrokerPort, endpoints["kafka"].TargetPort);
        Assert.Equal(RedPandaServerResource.KafkaInternalBrokerPort, endpoints["internal"].TargetPort);
        Assert.Equal(RedPandaServerResource.SchemaRegistryPort, endpoints["schemaregistry"].TargetPort);
        Assert.Equal(RedPandaServerResource.AdminPort, endpoints["admin"].TargetPort);
        Assert.Equal("http", endpoints["schemaregistry"].UriScheme);
        Assert.Equal("http", endpoints["admin"].UriScheme);
    }

    [Fact]
    public void AddRedPandaUsesProvidedHostPortForKafkaEndpoint()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddRedPanda("redpanda", port: 9092);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        RedPandaServerResource resource = Assert.Single(appModel.Resources.OfType<RedPandaServerResource>());
        EndpointAnnotation kafka = resource.GetEndpoint("kafka").EndpointAnnotation;
        Assert.Equal(9092, kafka.Port);
    }

    [Fact]
    public void ConnectionStringExpressionUsesKafkaEndpoint()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<RedPandaServerResource> redpanda = builder.AddRedPanda("redpanda");

        Assert.Equal(
            "{redpanda.bindings.kafka.host}:{redpanda.bindings.kafka.port}",
            redpanda.Resource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public void WithConsoleAddsConsoleResource()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddRedPanda("redpanda").WithConsole();

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        RedPandaConsoleContainerResource console = Assert.Single(appModel.Resources.OfType<RedPandaConsoleContainerResource>());
        Assert.Equal("redpanda-console", console.Name);

        Assert.True(console.TryGetLastAnnotation(out ContainerImageAnnotation? image));
        Assert.Equal("redpandadata/console", image.Image);
        Assert.Equal("v3.7.4", image.Tag);
    }

    [Fact]
    public void WithConsoleHostPortSetsEndpointPort()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddRedPanda("redpanda").WithConsole(console => console.WithHostPort(9090));

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        RedPandaConsoleContainerResource console = Assert.Single(appModel.Resources.OfType<RedPandaConsoleContainerResource>());
        EndpointAnnotation http = console.GetEndpoint("http").EndpointAnnotation;
        Assert.Equal(9090, http.Port);
    }

    [Fact]
    public void WithDataVolumeAddsVolumeAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddRedPanda("redpanda").WithDataVolume("redpanda-data");

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        RedPandaServerResource resource = Assert.Single(appModel.Resources.OfType<RedPandaServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<ContainerMountAnnotation>? mounts));
        ContainerMountAnnotation mount = Assert.Single(mounts!);
        Assert.Equal("redpanda-data", mount.Source);
        Assert.Equal("/var/lib/redpanda/data", mount.Target);
        Assert.Equal(ContainerMountType.Volume, mount.Type);
    }
}
