using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.OpenFga.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.OpenFga_AppHost> fixture) 
    : IClassFixture<AspireIntegrationTestFixture<Projects.OpenFga_AppHost>>
{
    [Fact]
    public async Task OpenFgaResourceStartsAndRespondsOk()
    {
        var resourceName = "openfga";
        
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));
        
        using var httpClient = fixture.CreateHttpClient(resourceName);
        
        var response = await httpClient.GetAsync("/healthz");
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
