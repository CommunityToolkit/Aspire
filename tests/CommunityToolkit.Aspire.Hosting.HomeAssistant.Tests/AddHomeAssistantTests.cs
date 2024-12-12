namespace CommunityToolkit.Aspire.Hosting.HomeAssistant.Tests;

public class AddHomeAssistantTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void AddHomeAssistantContainerWithDefaultsAddsAnnotationMetadata()
    {
        var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var homeAssistant = builder.AddHomeAssistant("home-assistant");

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<HomeAssistantResource>());
        Assert.Equal("home-assistant", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name is "http");
        Assert.Equal(8123, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Null(primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal(HomeAssistantContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(HomeAssistantContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(HomeAssistantContainerImageTags.Registry, containerAnnotation.Registry);
    }

    public static TheoryData<int> GetTenRandomPorts() => [.. Enumerable.Range(1, 10).Select(_ => Random.Shared.Next(1024, 65535))];

    [Theory]
    [MemberData(nameof(GetTenRandomPorts))]
    public void AddHomeAssistantContainerWithCustomPortAddsAnnotationMetadata(int port)
    {
        var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var homeAssistant = builder.AddHomeAssistant("home-assistant", port);

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var containerResource = Assert.Single(appModel.Resources.OfType<HomeAssistantResource>());
        Assert.Equal("home-assistant", containerResource.Name);

        var endpoints = containerResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Single(endpoints);

        var primaryEndpoint = Assert.Single(endpoints, e => e.Name is "http");
        Assert.Equal(8123, primaryEndpoint.TargetPort);
        Assert.False(primaryEndpoint.IsExternal);
        Assert.Equal("http", primaryEndpoint.Name);
        Assert.Equal(port, primaryEndpoint.Port);
        Assert.Equal(ProtocolType.Tcp, primaryEndpoint.Protocol);
        Assert.Equal("http", primaryEndpoint.Transport);
        Assert.Equal("http", primaryEndpoint.UriScheme);

        var containerAnnotation = Assert.Single(containerResource.Annotations.OfType<ContainerImageAnnotation>());

        Assert.Equal(HomeAssistantContainerImageTags.Tag, containerAnnotation.Tag);
        Assert.Equal(HomeAssistantContainerImageTags.Image, containerAnnotation.Image);
        Assert.Equal(HomeAssistantContainerImageTags.Registry, containerAnnotation.Registry);
    }

    [Fact]
    public async Task HomeAssistantCreatesConnectionString()
    {
        var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);

        var homeAssistant = builder
            .AddHomeAssistant("home-assistant")
            .WithEndpoint("http", e => e.AllocatedEndpoint = new AllocatedEndpoint(e, "localhost", 27420));

        using var app = builder.Build();

        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var connectionStringResource = Assert.Single(appModel.Resources.OfType<HomeAssistantResource>()) as IResourceWithConnectionString;
        var connectionString = await connectionStringResource.GetConnectionStringAsync();

        Assert.Equal($"Endpoint=http://localhost:27420", connectionString);
        Assert.Equal("Endpoint={home-assistant.bindings.http.scheme}://{home-assistant.bindings.http.host}:{home-assistant.bindings.http.port}", connectionStringResource.ConnectionStringExpression.ValueExpression);
    }
}
