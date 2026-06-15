using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Logto.Tests;

[RequiresDocker]
public class AppHostTest(
    AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Logto_AppHost> fixture
) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Logto_AppHost>>
{
    [Fact]
    public async Task LogtoResourceStartsAndRespondsOk()
    {
        const string resourceName = "logto";

        await fixture.ResourceNotificationService
            .WaitForResourceHealthyAsync(resourceName)
            .WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(resourceName);
        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
