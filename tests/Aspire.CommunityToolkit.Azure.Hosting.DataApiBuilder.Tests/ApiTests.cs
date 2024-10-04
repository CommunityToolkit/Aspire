using Aspire.CommunityToolkit.Hosting.DataApiBuilder.BlazorApp;
using Aspire.CommunityToolkit.Testing;
using FluentAssertions;
using Microsoft.TestUtilities;
using System.Net.Http.Json;

namespace Aspire.CommunityToolkit.Hosting.DataApiBuilder.Tests;

public class ApiTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost>>
{

    [ConditionalFact]
    [OSSkipCondition(OperatingSystems.Windows)]
    public async Task CanGetSeries()
    {

        await fixture.ResourceNotificationService.WaitForResourceAsync("dab", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(1));
        
        var httpClient = fixture.CreateHttpClient("dab");

        var response = await httpClient.GetAsync("/api/series");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var series = await response.Content.ReadFromJsonAsync<SeriesList>();
        series.Should().NotBeNull();
        series.value.Should().NotBeNull();
        series.value.Should().HaveCount(5);
    }


}

