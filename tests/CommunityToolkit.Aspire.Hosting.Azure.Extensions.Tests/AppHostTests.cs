using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Azure.Extensions.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Azure_Extensions_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Azure_Extensions_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        const string resourceName = "blobs-explorer";
        
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        
        using var httpClient = fixture.CreateHttpClient(resourceName);
        using var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
