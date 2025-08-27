using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_NodeJS_Extensions_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_NodeJS_Extensions_AppHost>>
{
    [Theory]
    [InlineData("vite-demo")]
    [InlineData("yarn-demo")]
    [InlineData("pnpm-demo")]
    [InlineData("turbo-web")]
    [InlineData("turbo-web-with-npx")]
    [InlineData("turbo-docs")]
    [InlineData("blog-monorepo")]
    [InlineData("blog-monorepo-with-npx")]
    public async Task ResourceStartsAndRespondsOk(string appName)
    {
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.ResourceNotificationService.WaitForResourceHealthyAsync(appName).WaitAsync(TimeSpan.FromMinutes(1));

        var response = await httpClient.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
