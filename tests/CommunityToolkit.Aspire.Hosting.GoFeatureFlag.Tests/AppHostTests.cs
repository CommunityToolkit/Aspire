using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using FluentAssertions;
using OpenFeature.Model;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.GoFeatureFlag.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_GoFeatureFlag_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_GoFeatureFlag_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "goff";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiServiceRetrieveFlags()
    {
        var resourceName = "apiservice";

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("goff").WaitAsync(TimeSpan.FromMinutes(5));
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var getResponse = await httpClient.GetAsync("/features/display-banner");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await getResponse.Content.ReadFromJsonAsync<ResolutionDetails<bool>>();
        Assert.NotNull(data);
        Assert.True(data.Value);
    }
}