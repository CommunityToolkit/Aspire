using CommunityToolkit.Aspire.Testing;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Hosting.Golang.Tests;

#pragma warning disable CTASPIRE001
public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Golang_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Golang_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var appName = "golang";
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.App.WaitForTextAsync("Listening and serving HTTP on :", appName).WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
