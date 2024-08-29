using CommunityToolkit.Aspire.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps.Tests;

public class SwaHostingComponentTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost>>
{
    [Fact]
    public async Task CanAccessFrontendSuccessfully()
    {
        var logger = fixture.App.Services.GetRequiredService<ILogger<SwaHostingComponentTests>>();
        logger.LogInformation("Starting test");
        var httpClient = fixture.CreateHttpClient("swa");

        logger.LogInformation("Waiting for resources to be ready");
        await fixture.ResourceNotificationService.WaitForResourceAsync("swa", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        logger.LogInformation("swa is ready");
        await fixture.ResourceNotificationService.WaitForResourceAsync("web", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        logger.LogInformation("web is ready");
        await fixture.ResourceNotificationService.WaitForResourceAsync("api", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        logger.LogInformation("api is ready");

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CanAccessApiSuccessfully()
    {
        var logger = fixture.App.Services.GetRequiredService<ILogger<SwaHostingComponentTests>>();
        logger.LogInformation("Starting test");
        var httpClient = fixture.CreateHttpClient("swa");

        logger.LogInformation("Waiting for resources to be ready");
        await fixture.ResourceNotificationService.WaitForResourceAsync("swa", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        logger.LogInformation("swa is ready");
        await fixture.ResourceNotificationService.WaitForResourceAsync("web", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        logger.LogInformation("web is ready");
        await fixture.ResourceNotificationService.WaitForResourceAsync("api", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        logger.LogInformation("api is ready");

        var response = await httpClient.GetAsync("/api/weather");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var forecasts = await response.Content.ReadFromJsonAsync<WeatherForecast[]>();
        Assert.NotNull(forecasts);
        forecasts.Length.Should().Be(6);
    }

    record WeatherForecast(DateTime Date, int TemperatureC, string Summary);
}