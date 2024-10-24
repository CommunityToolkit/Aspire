using CommunityToolkit.Aspire.Testing;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Hosting.Deno.Tests;

#pragma warning disable CTASPIRE001
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Deno_Apphost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Deno_Apphost>>
{
    [Theory]
    [InlineData("vite-demo")]
    public async Task ResourceStartsAndRespondsOk(string appName)
    {
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.App.WaitForTextAsync("VITE", appName).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("oak-demo")]
    public async Task ApiResourceStartsAndRespondsOk(string appName)
    {
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.App.WaitForTextAsync("Server listening on port ", appName).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/weather");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
