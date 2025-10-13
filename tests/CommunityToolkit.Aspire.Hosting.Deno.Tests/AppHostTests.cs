using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Deno.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Deno_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Deno_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var appName = "vite-demo";

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(appName)
            .WaitAsync(TimeSpan.FromSeconds(30));

        var httpClient = fixture.CreateHttpClient(appName);
        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiResourceStartsAndRespondsOk()
    {
        var appName = "oak-demo";

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(appName)
            .WaitAsync(TimeSpan.FromSeconds(30));

        var httpClient = fixture.CreateHttpClient(appName);
        var response = await httpClient.GetAsync("/weather");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
