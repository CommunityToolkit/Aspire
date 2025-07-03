using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.McpInspector.Tests;

public class McpInspectorResourceBuilderExtensionsTests
{
    [Fact]
    public void AddMcpInspectorWithDefaultsAddsResource()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var inspector = appBuilder.AddMcpInspector("inspector");

        using var app = appBuilder.Build();

        // Assert
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var inspectorResource = Assert.Single(appModel.Resources.OfType<McpInspectorResource>());
        Assert.Equal("inspector", inspectorResource.Name);

        var endpoints = inspectorResource.Annotations.OfType<EndpointAnnotation>();
        Assert.Equal(2, endpoints.Count());

        Assert.Single(endpoints, e => e.Name == "client");
        Assert.Single(endpoints, e => e.Name == "server-proxy");

        var annotations = inspector.Resource.Annotations;
        Assert.Contains(ManifestPublishingCallbackAnnotation.Ignore, annotations);
    }

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
    [InlineData(McpTransportType.Sse)]
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
}
