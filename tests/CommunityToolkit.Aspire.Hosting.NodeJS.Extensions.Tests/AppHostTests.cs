using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_NodeJS_Extensions_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_NodeJS_Extensions_AppHost>>
{
    [Theory]
    [InlineData("turbo-web")]
    [InlineData("turbo-docs")]
    [InlineData("blog-monorepo")]
    public async Task ResourceStartsAndRespondsOk(string appName)
    {
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(appName).WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
