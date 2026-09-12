using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;

namespace CommunityToolkit.Aspire.Hosting.Redis.Extensions.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Redis_Extensions_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Redis_Extensions_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "dbgate";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DbGateCanConnectToRedis()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("redis1", cts.Token);
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync("dbgate", cts.Token);

        var httpClient = fixture.CreateHttpClient("dbgate");

        using var loginResponse = await httpClient.PostAsJsonAsync(
            "/auth/login",
            new { amoid = "none" },
            cancellationToken: cts.Token);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(
            cts.Token);

        var accessToken = login.GetProperty("accessToken").GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var refreshResponse = await httpClient.PostAsJsonAsync(
            "/server-connections/refresh",
            new
            {
                conid = "redis1",
                keepOpen = true
            },
            cancellationToken: cts.Token);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        using var pingResponse = await httpClient.PostAsJsonAsync(
            "/database-connections/call-method",
            new
            {
                conid = "redis1",
                database = "db0",
                method = "ping",
                args = Array.Empty<object>()
            },
            cancellationToken: cts.Token);

        Assert.Equal(HttpStatusCode.OK, pingResponse.StatusCode);

        var pong = await pingResponse.Content.ReadFromJsonAsync<string>(
            cts.Token);

        Assert.Equal("PONG", pong);
    }
}
