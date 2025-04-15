using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;

namespace CommunityToolkit.Aspire.Hosting.Adminer.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Adminer_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Adminer_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "postgres1-adminer";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}