using CommunityToolkit.Aspire.Testing;
using Aspire.Components.Common.Tests;

namespace CommunityToolkit.Aspire.Hosting.SqlServer.Extensions.Tests;

[RequiresDocker]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SqlServer_Extensions_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_SqlServer_Extensions_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var resourceName = "sqlserver1-dbgate";
        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(resourceName).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient(resourceName);

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}