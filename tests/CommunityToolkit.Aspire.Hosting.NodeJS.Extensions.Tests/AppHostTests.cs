using CommunityToolkit.Aspire.Testing;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Hosting.NodeJS.Extensions.Tests;

#pragma warning disable CTASPIRE001
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_NodeJS_Extensions_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_NodeJS_Extensions_AppHost>>
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
