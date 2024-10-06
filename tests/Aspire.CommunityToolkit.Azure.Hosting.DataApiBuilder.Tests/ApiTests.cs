using Aspire.CommunityToolkit.Hosting.DataApiBuilder.BlazorApp;
using Aspire.CommunityToolkit.Testing;
using Aspire.Components.Common.Tests;
using FluentAssertions;
using System.Net.Http.Json;

namespace Aspire.CommunityToolkit.Hosting.DataApiBuilder.Tests;

[RequiresDocker]
public class ApiTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost>>
{
    [Fact]
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

