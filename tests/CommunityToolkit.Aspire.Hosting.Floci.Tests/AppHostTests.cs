using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Floci.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Floci_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Floci_AppHost>>
{
    private const string ResourceName = "floci";
    private const string UIResourceName = "floci-ui";

    [Fact]
    public async Task ResourceStartsAndBecomesHealthy()
    {
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(ResourceName)
            .WaitAsync(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public async Task UIResourceStartsAndBecomesHealthy()
    {
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(UIResourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));
    }
}
