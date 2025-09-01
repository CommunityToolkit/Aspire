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
        await fixture.ResourceNotificationService.WaitForResourceAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(1));
        var httpClient = fixture.CreateHttpClient(resourceName);
    }
}
