using System.Linq;
using System.Net.Sockets;
using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.Jellyfin;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Jellyfin.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddJellyfinBuilderShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = null!;
        Assert.Throws<ArgumentNullException>(() => builder.AddJellyfin("jellyfin"));
    }

    [Fact]
    public void AddJellyfinNameShouldNotBeNull()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddJellyfin(null!));
    }

    [Fact]
    public void AddJellyfinSetsContainerImageDetails()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<JellyfinContainerResource>().SingleOrDefault();
        Assert.NotNull(resource);
        Assert.Equal("jellyfin", resource.Name);

        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotation));
        Assert.Equal(JellyfinContainerImageTags.Registry, imageAnnotation!.Registry);
        Assert.Equal(JellyfinContainerImageTags.Image, imageAnnotation.Image);
        Assert.Equal(JellyfinContainerImageTags.Tag, imageAnnotation.Tag);
    }

    [Fact]
    public void AddJellyfinHttpEndpointTargetsPort8096()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var http = resource.Annotations.OfType<EndpointAnnotation>().Single(e => e.Name == "http");
        Assert.Equal(8096, http.TargetPort);
        Assert.Equal(ProtocolType.Tcp, http.Protocol);
    }

    [Fact]
    public void AddJellyfinDefaultsToPersistentLifetime()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        Assert.True(resource.TryGetLastAnnotation(out ContainerLifetimeAnnotation? lifetime));
        Assert.Equal(ContainerLifetime.Persistent, lifetime!.Lifetime);
    }

    [Fact]
    public void WithDataVolumeMountsConfigDirectory()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin").WithDataVolume("jellyfin-config");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var mount = resource.Annotations.OfType<ContainerMountAnnotation>()
            .Single(m => m.Target == "/config");
        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.Equal("jellyfin-config", mount.Source);
    }

    [Fact]
    public void WithDataBindMountMountsConfigDirectory()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin").WithDataBindMount("./local-config");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var mount = resource.Annotations.OfType<ContainerMountAnnotation>()
            .Single(m => m.Target == "/config");
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.NotNull(mount.Source);
    }

    [Fact]
    public void WithCacheVolumeMountsCacheDirectory()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin").WithCacheVolume("jellyfin-cache");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var mount = resource.Annotations.OfType<ContainerMountAnnotation>()
            .Single(m => m.Target == "/cache");
        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.Equal("jellyfin-cache", mount.Source);
    }

    [Fact]
    public void WithCacheBindMountMountsCacheDirectory()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin").WithCacheBindMount("./local-cache");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var mount = resource.Annotations.OfType<ContainerMountAnnotation>()
            .Single(m => m.Target == "/cache");
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
    }

    [Fact]
    public void WithMediaBindMountDefaultsToReadOnlyMediaTarget()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin").WithMediaBindMount("./media");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var mount = resource.Annotations.OfType<ContainerMountAnnotation>()
            .Single(m => m.Target == "/media");
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.True(mount.IsReadOnly);
    }

    [Fact]
    public void WithMediaBindMountSupportsMultipleLibrariesAtCustomTargets()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin")
            .WithMediaBindMount("./movies", target: "/movies")
            .WithMediaBindMount("./tv", target: "/tv", isReadOnly: false);

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var mounts = resource.Annotations.OfType<ContainerMountAnnotation>().ToList();
        var movies = mounts.Single(m => m.Target == "/movies");
        var tv = mounts.Single(m => m.Target == "/tv");

        Assert.True(movies.IsReadOnly);
        Assert.False(tv.IsReadOnly);
    }

    [Fact]
    public void WithFontsBindMountIsReadOnlyAtCustomFontsPath()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin").WithFontsBindMount("./fonts");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var mount = resource.Annotations.OfType<ContainerMountAnnotation>()
            .Single(m => m.Target == "/usr/local/share/fonts/custom");
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        Assert.True(mount.IsReadOnly);
    }

    [Fact]
    public async Task WithPublishedServerUrlSetsEnvironmentVariable()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin").WithPublishedServerUrl("http://media.example.com");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var envs = await resource.GetEnvironmentVariablesAsync();
        Assert.Equal("http://media.example.com", envs["JELLYFIN_PublishedServerUrl"]);
    }

    [Fact]
    public void WithDiscoveryEndpointAddsUdpEndpointOnPort7359()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin").WithDiscoveryEndpoint();

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == "discovery");
        Assert.Equal(ProtocolType.Udp, endpoint.Protocol);
        Assert.Equal(7359, endpoint.TargetPort);
    }

    [Fact]
    public void WithDlnaEndpointAddsUdpEndpointOnPort1900()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin").WithDlnaEndpoint();

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var endpoint = resource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == "dlna");
        Assert.Equal(ProtocolType.Udp, endpoint.Protocol);
        Assert.Equal(1900, endpoint.TargetPort);
    }

    [Fact]
    public void ConnectionPropertiesIncludeEndpointHostPortAndUri()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddJellyfin("jellyfin");

        using var app = builder.Build();
        var resource = app.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.OfType<JellyfinContainerResource>().Single();

        var withCs = (IResourceWithConnectionString)resource;
        var keys = withCs.GetConnectionProperties().Select(kv => kv.Key).ToArray();

        Assert.Contains("Endpoint", keys);
        Assert.Contains("Host", keys);
        Assert.Contains("Port", keys);
        Assert.Contains("Uri", keys);
    }
}
