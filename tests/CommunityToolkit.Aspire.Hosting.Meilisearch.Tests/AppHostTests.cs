using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Meilisearch.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Meilisearch_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Meilisearch_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "meilisearch";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiServiceCreateData()
    {
        var resourceName = "apiservice";

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("meilisearch").WaitAsync(TimeSpan.FromMinutes(5));
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var createResponse = await httpClient.GetAsync("/create").WaitAsync(TimeSpan.FromMinutes(5));
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var getResponse = await httpClient.GetAsync("/get");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var data = await getResponse.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(data);
        Assert.NotEmpty(data);

    }
}