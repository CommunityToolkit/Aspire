using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Mosquitto.Tests;

public class ResourceCreationTests
{
    [Fact]
    public void AddMosquittoShouldThrowWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddMosquitto("mqtt"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddMosquittoShouldThrowWhenNameIsNullOrEmpty(string? name)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        Assert.ThrowsAny<ArgumentException>(() => builder.AddMosquitto(name!));
    }

    [Fact]
    public void AddMosquittoSetsContainerImageAnnotations()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddMosquitto("mqtt");

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        MosquittoServerResource resource = Assert.Single(appModel.Resources.OfType<MosquittoServerResource>());
        Assert.Equal("mqtt", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? image));
        Assert.Equal("library/eclipse-mosquitto", image.Image);
        Assert.Equal("2.0.22", image.Tag);
        Assert.Equal("docker.io", image.Registry);
    }

    [Fact]
    public void AddMosquittoCreatesExpectedEndpoint()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddMosquitto("mqtt");

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        MosquittoServerResource resource = Assert.Single(appModel.Resources.OfType<MosquittoServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<EndpointAnnotation>? annotations));
        Dictionary<string, EndpointAnnotation> endpoints = annotations!.ToDictionary(e => e.Name);

        KeyValuePair<string, EndpointAnnotation> tcpEntry = Assert.Single(endpoints);
        Assert.Equal("tcp", tcpEntry.Key);
        Assert.Equal(MosquittoServerResource.DefaultPort, tcpEntry.Value.TargetPort);
        Assert.Equal(MosquittoServerResource.PrimaryEndpointScheme, tcpEntry.Value.UriScheme);
    }

    [Fact]
    public void AddMosquittoUsesProvidedHostPort()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddMosquitto("mqtt", port: 1883);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        MosquittoServerResource resource = Assert.Single(appModel.Resources.OfType<MosquittoServerResource>());
        EndpointAnnotation tcp = resource.GetEndpoint(MosquittoServerResource.PrimaryEndpointName).EndpointAnnotation;
        Assert.Equal(1883, tcp.Port);
    }

    [Fact]
    public void AddMosquittoDoesNotPinHostPortWhenPortIsNotProvided()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddMosquitto("mqtt");

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        MosquittoServerResource resource = Assert.Single(appModel.Resources.OfType<MosquittoServerResource>());
        EndpointAnnotation tcp = resource.GetEndpoint(MosquittoServerResource.PrimaryEndpointName).EndpointAnnotation;

        Assert.Null(tcp.Port);
        Assert.True(tcp.IsProxied);
    }

    [Fact]
    public void ConnectionStringExpressionUsesTcpEndpoint()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<MosquittoServerResource> mqtt = builder.AddMosquitto("mqtt");

        Assert.Equal(
            "mqtt://{mqtt.bindings.tcp.host}:{mqtt.bindings.tcp.port}",
            mqtt.Resource.ConnectionStringExpression.ValueExpression);
    }

    [Fact]
    public void UriExpressionMatchesConnectionStringExpression()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        IResourceBuilder<MosquittoServerResource> mqtt = builder.AddMosquitto("mqtt");

        Assert.Equal(
            mqtt.Resource.ConnectionStringExpression.ValueExpression,
            mqtt.Resource.UriExpression.ValueExpression);
    }

    [Fact]
    public void WithDataVolumeAddsVolumeAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddMosquitto("mqtt").WithDataVolume("mosquitto-data");

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        MosquittoServerResource resource = Assert.Single(appModel.Resources.OfType<MosquittoServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<ContainerMountAnnotation>? mounts));
        ContainerMountAnnotation mount = Assert.Single(mounts!);
        Assert.Equal("mosquitto-data", mount.Source);
        Assert.Equal("/mosquitto/data", mount.Target);
        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.False(mount.IsReadOnly);
    }

    [Fact]
    public void WithDataBindMountAddsBindMountAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddMosquitto("mqtt").WithDataBindMount("./mosquitto-data");

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        MosquittoServerResource resource = Assert.Single(appModel.Resources.OfType<MosquittoServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<ContainerMountAnnotation>? mounts));
        ContainerMountAnnotation mount = Assert.Single(mounts!);
        Assert.EndsWith("mosquitto-data", mount.Source);
        Assert.Equal("/mosquitto/data", mount.Target);
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.False(mount.IsReadOnly);
    }

    [Fact]
    public void AddMosquittoShipsDefaultConfigFile()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddMosquitto("mqtt");

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        MosquittoServerResource resource = Assert.Single(appModel.Resources.OfType<MosquittoServerResource>());

        Assert.True(resource.TryGetAnnotationsOfType(out IEnumerable<ContainerFileSystemCallbackAnnotation>? destinations));
        ContainerFileSystemCallbackAnnotation destination = Assert.Single(destinations!);
        Assert.Equal("/mosquitto/config", destination.DestinationPath);
    }
}
