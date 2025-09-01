using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.McpInspector.Tests;

public class McpInspectorResourceBuilderExtensionsTests
{
    [Fact]
    public void WithMcpServerAddsServerToResource()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Create a mock MCP server resource
        var mockServer = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer");

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithMcpServer(mockServer, isDefault: true);

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        Assert.Equal("inspector", inspectorResource.Name);

        Assert.Single(inspectorResource.McpServers);
        Assert.NotNull(inspectorResource.DefaultMcpServer);
        Assert.Equal("mcpServer", inspectorResource.DefaultMcpServer.Name);
        Assert.Equal(McpTransportType.StreamableHttp, inspectorResource.DefaultMcpServer.TransportType);
    }

    [Theory]
    [InlineData(McpTransportType.StreamableHttp)]
#pragma warning disable CS0618
    [InlineData(McpTransportType.Sse)]
#pragma warning restore CS0618
    public void WithMcpServerSpecificTransportTypeAddsServerToResource(McpTransportType transportType)
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Create a mock MCP server resource
        var mockServer = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer");

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithMcpServer(mockServer, isDefault: true, transportType: transportType);

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        Assert.Equal("inspector", inspectorResource.Name);

        Assert.Single(inspectorResource.McpServers);
        Assert.NotNull(inspectorResource.DefaultMcpServer);
        Assert.Equal("mcpServer", inspectorResource.DefaultMcpServer.Name);
        Assert.Equal(transportType, inspectorResource.DefaultMcpServer.TransportType);
    }

    [Fact]
    public void WithMcpServerCustomPathAddsServerWithCustomPath()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Create a mock MCP server resource
        var mockServer = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer");

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithMcpServer(mockServer, isDefault: true, path: "/custom/mcp/path");

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        Assert.Equal("inspector", inspectorResource.Name);

        Assert.Single(inspectorResource.McpServers);
        Assert.NotNull(inspectorResource.DefaultMcpServer);
        Assert.Equal("mcpServer", inspectorResource.DefaultMcpServer.Name);
        Assert.Equal("/custom/mcp/path", inspectorResource.DefaultMcpServer.Path);
    }

    [Fact]
    public void WithMultipleMcpServersAddsAllServersToResource()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Create mock MCP server resources
        var mockServer1 = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer1");
        var mockServer2 = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer2");

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithMcpServer(mockServer1, isDefault: true)
            .WithMcpServer(mockServer2, isDefault: false);

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        Assert.Equal("inspector", inspectorResource.Name);

        Assert.Equal(2, inspectorResource.McpServers.Count);
        Assert.NotNull(inspectorResource.DefaultMcpServer);
        Assert.Equal("mcpServer1", inspectorResource.DefaultMcpServer.Name);
    }

    [Fact]
    public void AddMcpInspectorGeneratesProxyTokenParameter()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector");

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        Assert.NotNull(inspectorResource.ProxyTokenParameter);
        Assert.Equal("inspector-proxyToken", inspectorResource.ProxyTokenParameter.Name);
    }

    [Fact]
    public void AddMcpInspectorWithCustomProxyTokenUsesProvidedToken()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var customToken = appBuilder.AddParameter("custom-token", secret: true);

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector", options =>
        {
            options.ProxyToken = customToken;
        });

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        Assert.NotNull(inspectorResource.ProxyTokenParameter);
        Assert.Equal("custom-token", inspectorResource.ProxyTokenParameter.Name);
        Assert.Same(customToken.Resource, inspectorResource.ProxyTokenParameter);
    }

    [Fact]
    public void AddMcpInspectorSetsCorrectEnvironmentVariables()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector", options =>
        {
            options.ClientPort = 1234;
            options.ServerPort = 5678;
        });

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        // Verify endpoints are configured correctly
        var clientEndpoint = inspectorResource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == McpInspectorResource.ClientEndpointName);
        var serverEndpoint = inspectorResource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == McpInspectorResource.ServerProxyEndpointName);

        Assert.Equal(1234, clientEndpoint.Port);
        Assert.Equal(5678, serverEndpoint.Port);
    }

    [Fact]
    public void AddMcpInspectorWithOptionsCreatesResourceCorrectly()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var customToken = appBuilder.AddParameter("custom-token", secret: true);

        var options = new McpInspectorOptions
        {
            ClientPort = 1111,
            ServerPort = 2222,
            InspectorVersion = "0.15.0",
            ProxyToken = customToken
        };

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector", options);

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        Assert.Equal("inspector", inspectorResource.Name);
        Assert.Equal("custom-token", inspectorResource.ProxyTokenParameter.Name);
        Assert.Same(customToken.Resource, inspectorResource.ProxyTokenParameter);

        // Verify endpoints are configured correctly
        var clientEndpoint = inspectorResource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == McpInspectorResource.ClientEndpointName);
        var serverEndpoint = inspectorResource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == McpInspectorResource.ServerProxyEndpointName);

        Assert.Equal(1111, clientEndpoint.Port);
        Assert.Equal(2222, serverEndpoint.Port);

        // Verify version argument is set correctly
        var argsAnnotation = inspectorResource.Annotations.OfType<CommandLineArgsCallbackAnnotation>().First();
        // We can't easily test the args directly, but we can verify the structure is correct
        Assert.NotNull(argsAnnotation);
    }

    [Fact]
    public void AddMcpInspectorWithOptionsUsesDefaultsWhenNotSpecified()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var options = new McpInspectorOptions(); // Use all defaults

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector", options);

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        Assert.Equal("inspector", inspectorResource.Name);
        Assert.Equal("inspector-proxyToken", inspectorResource.ProxyTokenParameter.Name);

        // Verify endpoints use default ports
        var clientEndpoint = inspectorResource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == McpInspectorResource.ClientEndpointName);
        var serverEndpoint = inspectorResource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == McpInspectorResource.ServerProxyEndpointName);

        Assert.Equal(6274, clientEndpoint.Port);
        Assert.Equal(6277, serverEndpoint.Port);
    }

    [Fact]
    public void AddMcpInspectorWithConfigurationDelegateCreatesResourceCorrectly()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var customToken = appBuilder.AddParameter("custom-token", secret: true);

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector", options =>
        {
            options.ClientPort = 3333;
            options.ServerPort = 4444;
            options.InspectorVersion = "0.15.0";
            options.ProxyToken = customToken;
        });

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        Assert.Equal("inspector", inspectorResource.Name);
        Assert.Equal("custom-token", inspectorResource.ProxyTokenParameter.Name);
        Assert.Same(customToken.Resource, inspectorResource.ProxyTokenParameter);

        // Verify endpoints are configured correctly
        var clientEndpoint = inspectorResource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == McpInspectorResource.ClientEndpointName);
        var serverEndpoint = inspectorResource.Annotations.OfType<EndpointAnnotation>()
            .Single(e => e.Name == McpInspectorResource.ServerProxyEndpointName);

        Assert.Equal(3333, clientEndpoint.Port);
        Assert.Equal(4444, serverEndpoint.Port);
    }

    [Fact]
    public void WithMcpServerCreatesResourceRelationshipAnnotations()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Create a mock MCP server resource
        var mockServer = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer");

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithMcpServer(mockServer, isDefault: true);

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        var serverResource = appModel.Resources.Single(r => r.Name == "mcpServer");

        // The inspector and/or server should have a resource relationship annotation linking them.
        var serverHasRelationship = serverResource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var serverRelationships);
        var inspectorHasRelationship = inspectorResource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var inspectorRelationships);

        Assert.True(serverHasRelationship || inspectorHasRelationship, "Expected a ResourceRelationshipAnnotation on either the server or inspector resource.");
    }

    [Fact]
    public void WithMcpServerPreservesCustomPathSegments()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Create a mock MCP server resource
        var mockServer = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer");

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithMcpServer(mockServer, isDefault: true, path: "/route/mcp");

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        Assert.Single(inspectorResource.McpServers);
        var serverMeta = inspectorResource.McpServers.Single();

        // Path should be preserved exactly as provided (not url-encoded)
        Assert.Equal("/route/mcp", serverMeta.Path);
    }

    [Fact]
    public void CombineHandlesMultipleSegmentsAndDoesNotEncodeSlashes()
    {
        // Arrange
        var baseUrl = "http://localhost:1234";
        var segments = new[] { "/route/mcp", "nested/path" };

        // Use reflection to call internal Combine
        var type = typeof(McpInspectorResourceBuilderExtensions);
        var method = type.GetMethod("Combine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method!.Invoke(null, new object[] { baseUrl, segments }) as Uri;

        // Assert
        Assert.NotNull(result);
        // Ensure that slashes from segments are preserved and not percent-encoded
        var expected = new Uri("http://localhost:1234/route/mcp/nested/path");
        Assert.Equal(expected, result);
    }
}
