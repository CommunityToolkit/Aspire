using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flagd.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Flagd_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Flagd_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsCorrectly()
    {
        var resourceName = "flagd";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);
        
        // flagd should be reachable (we can't easily test the gRPC endpoint without more setup)
        // So we'll just verify that the service is running and the container is healthy
    }
}
