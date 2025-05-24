//using CommunityToolkit.Aspire.Testing;
//using System.Net.Http.Json;

//namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps.Tests;

//public class SwaHostingComponentTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_StaticWebApps_AppHost>>
//{
//    [Fact(Skip = "Test is unstable in CI")]
//    public async Task CanAccessFrontendSuccessfully()
//    {
//        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

//        var httpClient = fixture.CreateHttpClient("swa");

//        var ct = cts.Token;
//        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("swa", ct);

//        var response = await httpClient.GetAsync("/", ct);

//        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
//    }

//    [Fact(Skip = "Test is unstable in CI")]
//    public async Task CanAccessApiSuccessfully()
//    {
//        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
//        var httpClient = fixture.CreateHttpClient("swa");

//        var ct = cts.Token;
//        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("swa", ct);

//        var response = await httpClient.GetAsync("/api/weather", ct);

//        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
//        var forecasts = await response.Content.ReadFromJsonAsync<WeatherForecast[]>(ct);
//        Assert.NotNull(forecasts);
//        Assert.Equal(6, forecasts.Length);
//    }

//    record WeatherForecast(DateTime Date, int TemperatureC, string Summary);
//}