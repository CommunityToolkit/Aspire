using CommunityToolkit.Aspire.Testing;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps.Tests;

[Collection("Integration Tests")]
public class SwaHostingComponentTests(ITestOutputHelper testOutput) : AspireIntegrationTest<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost>(testOutput)
{
    [Fact]
    public async Task EmulatorLaunchesOnDefaultPort()
    {
        var httpClient = app.CreateHttpClient("swa");

        await ResourceNotificationService.WaitForResourceAsync("swa", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));
        await ResourceNotificationService.WaitForResourceAsync("web", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));
        await ResourceNotificationService.WaitForResourceAsync("api", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CanAccessApi()
    {
        var httpClient = app.CreateHttpClient("swa");

        await ResourceNotificationService.WaitForResourceAsync("swa", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));
        await ResourceNotificationService.WaitForResourceAsync("web", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));
        await ResourceNotificationService.WaitForResourceAsync("api", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/api/weather");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var forecasts = await response.Content.ReadFromJsonAsync<WeatherForecast[]>();
        Assert.NotNull(forecasts);
        forecasts.Length.Should().Be(6);
    }

    record WeatherForecast(DateTime Date, int TemperatureC, string Summary);
}