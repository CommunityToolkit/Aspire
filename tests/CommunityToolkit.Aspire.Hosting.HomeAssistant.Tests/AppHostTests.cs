namespace CommunityToolkit.Aspire.Hosting.HomeAssistant.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_HomeAssistant_AppHost> fixture) 
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_HomeAssistant_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var appName = "home-assistant";

        var rns = fixture.App.Services.GetRequiredService<ResourceNotificationService>();
        _ = await rns.WaitForResourceHealthyAsync(appName).WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(appName);
        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
