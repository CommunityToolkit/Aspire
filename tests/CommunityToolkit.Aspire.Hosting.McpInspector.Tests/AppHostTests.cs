using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.McpInspector.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_McpInspector_AppHost>>
{
    [Theory]
    [InlineData(McpInspectorResource.ClientEndpointName, "/")]
    [InlineData(McpInspectorResource.ServerProxyEndpointName, "/config")]
    public async Task ResourceStartsAndRespondsOk(string endpointName, string route)
    {
        var resourceName = "mcp-inspector";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName, endpointName: endpointName);

        var response = await httpClient.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
