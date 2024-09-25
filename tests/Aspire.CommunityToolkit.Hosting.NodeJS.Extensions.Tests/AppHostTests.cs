using Aspire.CommunityToolkit.Testing;
using FluentAssertions;

namespace Aspire.CommunityToolkit.Hosting.NodeJS.Extensions.Tests;

#pragma warning disable CTASPIRE001
public class AppHostTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_NodeJS_Extensions_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_NodeJS_Extensions_AppHost>>
{
    [Theory]
    [InlineData("vite-demo")]
    [InlineData("yarn-demo")]
    [InlineData("pnpm-demo")]
    public async Task ResourceStartsAndRespondsOk(string appName)
    {
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.App.WaitForTextAsync("VITE", appName).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
