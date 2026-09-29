using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Posta.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Posta_AppHost> fixture)
    : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Posta_AppHost>>
{
    private const string ResourceName = "posta";

    [Fact]
    public async Task ResourceStartsAndHealthEndpointResponds()
    {
        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(ResourceName)
            .WaitAsync(TimeSpan.FromMinutes(3));

        var httpEndpoint = fixture.GetEndpoint(ResourceName, "http");
        using var httpClient = new HttpClient { BaseAddress = httpEndpoint };

        using var response = await httpClient.GetAsync("/healthz", TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
    }
}