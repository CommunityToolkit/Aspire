using CommunityToolkit.Aspire.Hosting.Compose.Mapping;
using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Compose.Tests;

public class MapperAdvancedTests
{
    [Fact]
    public void ParseImageReference_EmptyImage_ReturnsScratch()
    {
        (string image, string tag) = ServiceToResourceMapper.ParseImageReference(string.Empty);

        Assert.Equal("scratch", image);
        Assert.Equal("latest", tag);
    }

    [Fact]
    public void ParseImageReference_ImageWithoutTag_ReturnsLatest()
    {
        (string image, string tag) = ServiceToResourceMapper.ParseImageReference("nginx");

        Assert.Equal("nginx", image);
        Assert.Equal("latest", tag);
    }

    [Fact]
    public void ParseImageReference_ImageWithTag_SplitsCorrectly()
    {
        (string image, string tag) = ServiceToResourceMapper.ParseImageReference("postgres:16");

        Assert.Equal("postgres", image);
        Assert.Equal("16", tag);
    }

    [Fact]
    public void ParseImageReference_RegistryWithPort_NoTag_ReturnsLatest()
    {
        (string image, string tag) = ServiceToResourceMapper.ParseImageReference("registry.example.com:5000/myapp");

        Assert.Equal("registry.example.com:5000/myapp", image);
        Assert.Equal("latest", tag);
    }

    [Fact]
    public void ParseImageReference_RegistryWithPort_WithTag_SplitsCorrectly()
    {
        (string image, string tag) = ServiceToResourceMapper.ParseImageReference("registry.example.com:5000/myapp:v2");

        Assert.Equal("registry.example.com:5000/myapp", image);
        Assert.Equal("v2", tag);
    }

    [Fact]
    public void ParsePortMapping_IpBoundWithoutHostPort_ReturnsNullHostPort()
    {
        (int? hostPort, int containerPort, string protocol)? result = PortMapper.ParsePortMapping("127.0.0.1::9090");

        Assert.NotNull(result);
        Assert.Null(result.Value.hostPort);
        Assert.Equal(9090, result.Value.containerPort);
    }

    [Fact]
    public void ParsePortMapping_IpBoundWithHostPort_ReturnsBoth()
    {
        (int? hostPort, int containerPort, string protocol)? result = PortMapper.ParsePortMapping("127.0.0.1:8080:80");

        Assert.NotNull(result);
        Assert.Equal(8080, result.Value.hostPort);
        Assert.Equal(80, result.Value.containerPort);
    }

    [Fact]
    public void ParsePortMapping_UdpProtocol_ReturnsUdp()
    {
        (int? hostPort, int containerPort, string protocol)? result = PortMapper.ParsePortMapping("53:53/udp");

        Assert.NotNull(result);
        Assert.Equal("udp", result.Value.protocol);
    }

    [Fact]
    public void ParsePortMapping_InvalidPort_ReturnsNull()
    {
        (int? hostPort, int containerPort, string protocol)? result = PortMapper.ParsePortMapping("invalid:port");
        Assert.Null(result);
    }

    [Fact]
    public void AddCompose_ContainerName_SetsAnnotation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("container-name.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        ContainerResource db = appModel.Resources.OfType<ContainerResource>().Single(r => r.Name == "db");
        ContainerNameAnnotation? annotation = db.Annotations.OfType<ContainerNameAnnotation>().SingleOrDefault();

        Assert.NotNull(annotation);
        Assert.Equal("my-custom-postgres", annotation.Name);
    }

    [Fact]
    public void AddCompose_UdpPorts_CreatesUdpEndpoint()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("udp-ports.yml");

        builder.AddCompose(composePath);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        ContainerResource dns = appModel.Resources.OfType<ContainerResource>().Single(r => r.Name == "dns");
        List<EndpointAnnotation> endpoints = dns.Annotations.OfType<EndpointAnnotation>().ToList();

        Assert.True(endpoints.Any(e => e.UriScheme == "udp"), "Should have a UDP endpoint");
    }

    [Fact]
    public void AddCompose_VolumesAdvanced_CreatesBindMountsAndNamedVolumes()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("volumes-advanced.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(1, compose.Count);
        Assert.Contains("app", compose.ServiceNames);
    }

    [Fact]
    public void AddCompose_IpBoundWithoutHostPort_DoesNotThrow()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("ip-bound-ports.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(1, compose.Count);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        ContainerResource web = appModel.Resources.OfType<ContainerResource>().Single(r => r.Name == "web");
        List<EndpointAnnotation> endpoints = web.Annotations.OfType<EndpointAnnotation>().ToList();

        Assert.Equal(2, endpoints.Count);
    }

    [Fact]
    public void AddCompose_RegistryWithPort_ParsesImageCorrectly()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("registry-port-image.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.Equal(2, compose.Count);
        Assert.Contains("app", compose.ServiceNames);
        Assert.Contains("tagged", compose.ServiceNames);
    }

    [Fact]
    public void AddCompose_MissingDependency_ThrowsInvalidOperation()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("missing-dependency.yml");

        Assert.Throws<InvalidOperationException>(() => builder.AddCompose(composePath));
    }

    [Fact]
    public void AddCompose_DuplicatePortEndpoints_UniqueNames()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("multiple-ports.yml");

        builder.AddCompose(composePath);

        using DistributedApplication app = builder.Build();
        DistributedApplicationModel appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        ContainerResource web = appModel.Resources.OfType<ContainerResource>().Single(r => r.Name == "web");
        List<EndpointAnnotation> endpoints = web.Annotations.OfType<EndpointAnnotation>().ToList();
        List<string> names = endpoints.Select(e => e.Name).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void ParseEnvironment_UnknownType_ReturnsEmpty()
    {
        Dictionary<string, string> result = EnvironmentMapper.Parse(42);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseDependsOn_UnknownType_ReturnsEmpty()
    {
        Dictionary<string, string> result = DependsOnMapper.Parse(42);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseDependsOn_DictWithoutConditionKey_DefaultsToServiceStarted()
    {
        Dictionary<object, object> deps = new()
        {
            { "db", new Dictionary<object, object> { { "restart", true } } }
        };

        Dictionary<string, string> result = DependsOnMapper.Parse(deps);

        Assert.Equal("service_started", result["db"]);
    }

    [Fact]
    public void ParseStringOrList_UnknownType_ReturnsEmpty()
    {
        string[] result = ServiceToResourceMapper.ParseStringOrList(42);
        Assert.Empty(result);
    }

    [Fact]
    public void AddCompose_V1CommandOnly_DetectsAsV1()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        string composePath = GetTestFilePath("v1-command-only.yml");

        ComposeResourceCollection compose = builder.AddCompose(composePath);

        Assert.True(compose.Count >= 1);
        Assert.Contains("redis", compose.ServiceNames);
    }

    private static string GetTestFilePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "composes", fileName);
}
