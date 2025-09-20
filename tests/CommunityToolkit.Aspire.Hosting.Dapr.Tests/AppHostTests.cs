using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Dapr_AppHost> fixture) :
    IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Dapr_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "servicea";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName);
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/weatherforecast");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ServiceWithReferencedComponentsRespondsOk()
    {
        // This test verifies that services can correctly use Dapr components
        // that are referenced via the sidecar
        
        // ServiceB uses pubSub
        var resourceName = "serviceb";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName);
        var httpClient = fixture.CreateHttpClient(resourceName);
        
        var response = await httpClient.GetAsync("/weatherforecast");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
