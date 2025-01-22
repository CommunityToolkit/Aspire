using CommunityToolkit.Aspire.Testing;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps.Tests;

public class SwaHostingComponentTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost>>
{
    [Fact]
    public async Task CanAccessFrontendSuccessfully()
    {
        var httpClient = fixture.CreateHttpClient("swa");

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("swa").WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CanAccessApiSuccessfully()
    {
        var httpClient = fixture.CreateHttpClient("swa");

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("swa").WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/api/weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var forecasts = await response.Content.ReadFromJsonAsync<WeatherForecast[]>();
        Assert.NotNull(forecasts);
        Assert.Equal(6, forecasts.Length);
    }

    record WeatherForecast(DateTime Date, int TemperatureC, string Summary);
}