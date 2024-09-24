#if NET8_0
using CommunityToolkit.Aspire.Testing;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps.Tests;

#pragma warning disable CTASPIRE001
public class SwaHostingComponentTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost>>
{
    [Fact]
    public async Task CanAccessFrontendSuccessfully()
    {
        var httpClient = fixture.CreateHttpClient("swa");

        await fixture.App.WaitForTextAsync("Azure Static Web Apps emulator started", "swa").WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CanAccessApiSuccessfully()
    {
        var httpClient = fixture.CreateHttpClient("swa");

        await fixture.App.WaitForTextAsync("Azure Static Web Apps emulator started", "swa").WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/api/weather");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var forecasts = await response.Content.ReadFromJsonAsync<WeatherForecast[]>();
        Assert.NotNull(forecasts);
        forecasts.Length.Should().Be(6);
    }

    record WeatherForecast(DateTime Date, int TemperatureC, string Summary);
}
#endif