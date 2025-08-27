using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.McpInspector.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_AppHost>>
{
    [Fact]
    public async Task ClientEndpointStartsAndRespondsOk()
    {
        var resourceName = "mcp-inspector";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName, endpointName: McpInspectorResource.ClientEndpointName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ServerProxyConfigEndpointWithAuthRespondsOk()
    {
        var resourceName = "mcp-inspector";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));

        // Get the MCP Inspector resource to access the proxy token parameter
        var appModel = fixture.App.Services.GetRequiredService<DistributedApplicationModel>();
        var mcpInspectorResource = appModel.Resources.OfType<McpInspectorResource>().Single(r => r.Name == resourceName);

        // Get the token value
        var token = await mcpInspectorResource.ProxyTokenParameter.GetValueAsync(CancellationToken.None);

        var httpClient = fixture.CreateHttpClient(resourceName, endpointName: McpInspectorResource.ServerProxyEndpointName);

        // Add the Bearer token header for authentication
        httpClient.DefaultRequestHeaders.Add("X-MCP-Proxy-Auth", $"Bearer {token}");

        var response = await httpClient.GetAsync("/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
