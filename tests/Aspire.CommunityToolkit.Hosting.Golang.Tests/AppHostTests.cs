using Aspire.CommunityToolkit.Testing;
using FluentAssertions;

namespace Aspire.CommunityToolkit.Hosting.Golang.Tests;

#pragma warning disable CTASPIRE001
public class AppHostTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Golang_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Golang_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var appName = "golang";
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.App.WaitForTextAsync("Listening and serving HTTP on :8080", appName).WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
