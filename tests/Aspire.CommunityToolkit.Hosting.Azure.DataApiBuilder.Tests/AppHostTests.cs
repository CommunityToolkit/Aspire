using Aspire.CommunityToolkit.Hosting.Azure.DataApiBuilder.BlazorApp;
using Aspire.CommunityToolkit.Testing;
using Aspire.Components.Common.Tests;
using FluentAssertions;
using System.Net.Http.Json;

namespace Aspire.CommunityToolkit.Hosting.Azure.DataApiBuilder.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Azure_DataApiBuilder_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Azure_DataApiBuilder_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        await fixture.ResourceNotificationService.WaitForResourceAsync("dab", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(1));
        var httpClient = fixture.CreateHttpClient("dab");

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CanGetSeries()
    {

        await fixture.ResourceNotificationService.WaitForResourceAsync("dab", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(1));

        var httpClient = fixture.CreateHttpClient("dab");

        var response = await httpClient.GetAsync("/api/series");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var series = await response.Content.ReadFromJsonAsync<SeriesList>();
        // Using "standard" assertions because that cascades nullability state whereas FluentAssertions does not
        Assert.NotNull(series);
        series.value.Should().NotBeNull();
        series.value.Should().HaveCount(5);
    }
}