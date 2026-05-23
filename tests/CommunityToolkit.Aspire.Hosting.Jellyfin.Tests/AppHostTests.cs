using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Jellyfin.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Jellyfin_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Jellyfin_AppHost>>
{
    private const string ResourceName = "jellyfin";

    [Fact]
    public async Task ResourceStartsAndPublicSystemInfoIsReachable()
    {
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(ResourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(ResourceName, "http");

        using var response = await httpClient.GetAsync("/System/Info/Public");
        response.EnsureSuccessStatusCode();
    }
}
