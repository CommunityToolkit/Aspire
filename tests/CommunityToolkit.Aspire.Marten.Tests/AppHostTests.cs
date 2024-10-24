using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using FluentAssertions;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Marten.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Marten_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Marten_AppHost>>
{
    [Fact]
    public async Task ApiServiceCreateData()
    {
        var resourceName = "communitytoolkit-aspire-marten-apiservice";
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