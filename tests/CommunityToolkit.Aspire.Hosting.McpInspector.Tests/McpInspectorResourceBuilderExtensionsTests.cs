using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.JavaScript;

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

        // The server should have an "Inspected By" relationship pointing at the inspector
        Assert.True(serverResource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var serverRelationships));
        var serverRelationship = Assert.Single(serverRelationships);
        Assert.Same(inspectorResource, serverRelationship.Resource);
        Assert.Equal("Inspected By", serverRelationship.Type);

        // The inspector should have relationships back to the server: "Inspecting" and (since isDefault=true) "Default Inspected Server"
        Assert.True(inspectorResource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var inspectorRelationships));
        Assert.Contains(inspectorRelationships, r => r.Resource == serverResource && r.Type == "Inspecting");
        Assert.Contains(inspectorRelationships, r => r.Resource == serverResource && r.Type == "Default Inspected Server");
    }

    [Fact]
    public void WithMcpServer_NonDefaultDoesNotAddDefaultInspectedServerRelationship()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Create mock MCP server resources
        var mockServer1 = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer1");
        var mockServer2 = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer2");

        // Act: first is default, second is not
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithMcpServer(mockServer1, isDefault: true)
            .WithMcpServer(mockServer2, isDefault: false);

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        var serverResource2 = appModel.Resources.Single(r => r.Name == "mcpServer2");

        // Inspector should have relationships but should NOT have "Default Inspected Server" for serverResource2
        Assert.True(inspectorResource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var inspectorRelationships));
        Assert.DoesNotContain(inspectorRelationships, r => r.Resource == serverResource2 && r.Type == "Default Inspected Server");
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

    [Fact]
    public void WithMcpServerSupportsHttpsEndpoint()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Create a mock MCP server resource with https endpoint (uses name "https")
        var mockServer = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer")
            .WithHttpsEndpoint(name: "https");

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

        // Verify that the endpoint was successfully resolved (https should be preferred)
        var serverMetadata = inspectorResource.McpServers.Single();
        Assert.NotNull(serverMetadata.Endpoint);
        Assert.Equal("https", serverMetadata.Endpoint.EndpointName);
    }

    [Fact]
    public void WithMcpServerPrefersHttpsOverHttp()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Create a mock MCP server resource with both https and http endpoints
        // AddProject creates "http" by default, we add "https" with explicit name
        var mockServer = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer")
            .WithHttpsEndpoint(name: "https");

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithMcpServer(mockServer, isDefault: true);

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        var serverMetadata = inspectorResource.McpServers.Single();

        // Verify the endpoint is the https one (https should be preferred)
        Assert.Equal("https", serverMetadata.Endpoint.EndpointName);
    }

    [Fact]
    public void WithMcpServerWithBothEndpointsUsesHttps()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // AddProject creates both http and https endpoints by default
        var mockServer = appBuilder.AddProject<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_McpServer>("mcpServer");

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithMcpServer(mockServer, isDefault: true);

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        var serverMetadata = inspectorResource.McpServers.Single();

        // Verify the endpoint is the https one (preferred when both exist)
        Assert.Equal("https", serverMetadata.Endpoint.EndpointName);
    }

    [Fact]
    public void AddMcpInspectorDefaultsToNpx()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector");

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        // Default command is npx (set in constructor)
        Assert.Equal("npx", inspectorResource.Command);
    }

    [Fact]
    public void WithYarnSetsPackageManagerAnnotation()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithYarn();

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        // Verify the JavaScriptPackageManagerAnnotation is set with yarn
        Assert.True(inspectorResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pmAnnotation), 
            "JavaScriptPackageManagerAnnotation should be present after calling WithYarn()");
        Assert.Equal("yarn", pmAnnotation.ExecutableName);
    }

    [Fact]
    public void WithPnpmSetsPackageManagerAnnotation()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector")
            .WithPnpm();

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        // Verify the JavaScriptPackageManagerAnnotation is set with pnpm
        Assert.True(inspectorResource.TryGetLastAnnotation<JavaScriptPackageManagerAnnotation>(out var pmAnnotation), 
            "JavaScriptPackageManagerAnnotation should be present after calling WithPnpm()");
        Assert.Equal("pnpm", pmAnnotation.ExecutableName);
    }

    [Fact]
    public async Task WithYarnSetsCorrectArguments()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector", options =>
        {
            options.InspectorVersion = "0.15.0";
        })
            .WithYarn();

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        var args = await inspectorResource.GetArgumentValuesAsync();
        var argsList = args.ToList();

        // For yarn, the first arg should be "dlx"
        Assert.Equal("dlx", argsList[0]);
        Assert.Equal("@modelcontextprotocol/inspector@0.15.0", argsList[1]);
    }

    [Fact]
    public async Task WithPnpmSetsCorrectArguments()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector", options =>
        {
            options.InspectorVersion = "0.15.0";
        })
            .WithPnpm();

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        var args = await inspectorResource.GetArgumentValuesAsync();
        var argsList = args.ToList();

        // For pnpm, the first arg should be "dlx"
        Assert.Equal("dlx", argsList[0]);
        Assert.Equal("@modelcontextprotocol/inspector@0.15.0", argsList[1]);
    }

    [Fact]
    public async Task DefaultNpxUsesCorrectArguments()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector", options =>
        {
            options.InspectorVersion = "0.15.0";
        });

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());

        var args = await inspectorResource.GetArgumentValuesAsync();
        var argsList = args.ToList();

        // For npm/npx, the first arg should be "-y"
        Assert.Equal("-y", argsList[0]);
        Assert.Equal("@modelcontextprotocol/inspector@0.15.0", argsList[1]);
    }
}
