using Aspire.CommunityToolkit.Testing;
using Aspire.Components.Common.Tests;
using FluentAssertions;
using System.Net.Http.Json;

namespace Aspire.CommunityToolkit.Hosting.Meilisearch.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Meilisearch_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Meilisearch_AppHost>>
{
    [Theory]
    [InlineData("meilisearch")]
    public async Task ResourceStartsAndRespondsOk(string resourceName)
    {
        await fixture.ResourceNotificationService.WaitForResourceAsync(resourceName, KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(1));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiServiceCreateData()
    {
        var resourceName = "apiservice";
        await fixture.ResourceNotificationService.WaitForResourceAsync(resourceName, KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(1));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var createResponse = await httpClient.GetAsync("/create");
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await httpClient.GetAsync("/get");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await getResponse.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(data);
        Assert.NotEmpty(data);

    }
}