using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Dbx.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Dbx_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Dbx_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "dbx";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}