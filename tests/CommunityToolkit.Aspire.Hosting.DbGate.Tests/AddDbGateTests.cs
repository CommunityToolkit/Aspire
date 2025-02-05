using System.Net.Sockets;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.DbGate.Tests;
public class AddDbGateTests
{
    [Fact]
    public void AddDbGateContainerWithDefaultsAddsAnnotationMetadata()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbgate = appBuilder.AddDbGate("dbgate");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(3000, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(DbGateContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(DbGateContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(DbGateContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = dbgate.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public void AddDbGateContainerWithPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbgate = appBuilder.AddDbGate("dbgate", 9090);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(3000, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Equal(9090, primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(DbGateContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(DbGateContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(DbGateContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = dbgate.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Fact]
    public void MultipleAddDbGateCallsShouldAddOneDbGateResource()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        appBuilder.AddDbGate("dbgate1");
        appBuilder.AddDbGate("dbgate2");

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate1", containerResource.Name);
    }

    [Fact]
    public void VerifyWithHostPort()
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbgate = appBuilder.AddDbGate("dbgate").WithHostPort(9090);

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name == "http");
        Assert.Equal(3000, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Equal(9090, primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal(DbGateContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(DbGateContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(DbGateContainerImageTags.Registry, containerAnnotation.Registry);

        var annotations = dbgate.Resource.Annotations;

        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VerifyWithData(bool useVolume)
    {
        var appBuilder = DistributedApplication.CreateBuilder();

        var dbgate = appBuilder.AddDbGate("dbgate");

        if (useVolume)
        {
            dbgate.WithDataVolume("dbgate-data");
        }
        else
        {
            dbgate.WithDataBindMount("/data/dbgate");
        }

        using var app = appBuilder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<DbGateContainerResource>());
        Assert.Equal("dbgate", containerResource.Name);

        var mountAnnotations = containerResource.Annotations.OfType<ContainerMountAnnotation>();
        var mountAnnotation = Assert.Single(mountAnnotations);
        Assert.Equal("/root/.dbgate", mountAnnotation.Target);
        if (useVolume)
        {
            Assert.Equal("dbgate-data", mountAnnotation.Source);
            Assert.Equal(ContainerMountType.Volume, mountAnnotation.Type);
            Assert.False(mountAnnotation.IsReadOnly);
        }
        else
        {
            Assert.Equal(Path.GetFullPath("/data/dbgate", appBuilder.AppHostDirectory), mountAnnotation.Source);
            Assert.Equal(ContainerMountType.BindMount, mountAnnotation.Type);
            Assert.False(mountAnnotation.IsReadOnly);
        }
    }
}
