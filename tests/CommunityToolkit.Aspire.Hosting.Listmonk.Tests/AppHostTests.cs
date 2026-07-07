using CommunityToolkit.Aspire.Testing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.Listmonk.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Listmonk_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Listmonk_AppHost>>
{
    [Fact]
    public async Task ListmonkResourceStartsAndRespondsOk()
    {
        const string resourceName = "listmonk";
        var @event = await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(2));

        Assert.Equal(HealthStatus.Healthy, @event.Snapshot.HealthStatus);
    }
}
