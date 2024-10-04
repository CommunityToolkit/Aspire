using Aspire.CommunityToolkit.Hosting.DataApiBuilder.BlazorApp;
using Aspire.CommunityToolkit.Testing;
using FluentAssertions;
using Microsoft.TestUtilities;
using System.Net.Http.Json;

namespace Aspire.CommunityToolkit.Hosting.DataApiBuilder.Tests;

public class ApiTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost>>
{

    [ConditionalFact]
    
    public async Task CanGetSeries()
    {

        await fixture.ResourceNotificationService.WaitForResourceAsync("dab", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(1));
        
        var httpClient = fixture.CreateHttpClient("dab", "http");

        //Error: Unable to find config file: dab-config.json does not exist.

        var response = await httpClient.GetAsync("/api/series");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var series = await response.Content.ReadFromJsonAsync<SeriesList>();
        series.Should().NotBeNull();
        series.value.Should().NotBeNull();
        series.value.Should().HaveCount(5);
    }


}

