using CommunityToolkit.Aspire.Testing;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Hosting.Deno.Tests;

#pragma warning disable CTASPIRE001
[ActiveIssue("https://github.com/CommunityToolkit/Aspire/issues/377")]
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Deno_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Deno_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var appName = "vite-demo";
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.App.WaitForTextAsync("VITE", appName).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiResourceStartsAndRespondsOk()
    {
        var appName = "oak-demo";
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.App.WaitForTextAsync("Server listening on port ", appName).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/weather");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
