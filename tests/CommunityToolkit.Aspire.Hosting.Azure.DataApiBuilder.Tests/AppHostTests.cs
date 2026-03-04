using CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.BlazorApp;
using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Azure_DataApiBuilder_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Azure_DataApiBuilder_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("dab").WaitAsync(TimeSpan.FromMinutes(1));
        var httpClient = fixture.CreateHttpClient("dab");

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CanGetSeries()
    {
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("dab").WaitAsync(TimeSpan.FromMinutes(1));

        var httpClient = fixture.CreateHttpClient("dab");

        var response = await httpClient.GetAsync("/api/series");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var series = await response.Content.ReadFromJsonAsync<DabResponse<Series>>();
        Assert.NotNull(series);
        Assert.Equal(5, series.Value!.Count);
    }
}