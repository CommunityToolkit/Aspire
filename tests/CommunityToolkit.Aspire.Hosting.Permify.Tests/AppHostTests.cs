using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Permify.Tests;

[RequiresDocker]
public class AppHostTests(
    AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Permify_AppHost> fixture
) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Permify_AppHost>>
{
    [Fact]
    public async Task Permify_Starts_And_Responds()
    {
        var resourceName = "permify";

        await fixture.ResourceNotificationService
            .WaitForResourceAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(resourceName);
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tenants/t1/schemas/write");
        request.Content = JsonContent.Create(new
        {
            schema = "entity user {}"
        });

        var response = await httpClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}