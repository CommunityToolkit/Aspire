using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using System.Net;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.N8n.Tests;

[RequiresDocker]
public class AppHostTests(
    AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_N8n_AppHost> fixture
) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_N8n_AppHost>>
{
    [Fact]
    public async Task N8n_Starts_And_Responds_Ok()
    {
        var resourceName = "n8n";

        // Wait for N8n to be healthy (it has a health check configured)
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(resourceName);

        // Test the health endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        // Needs to match the external domain for N8n or we get a 404
        request.Headers.Host = $"{fixture.App.GetEndpoint(resourceName, "http").Host}";
        var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
