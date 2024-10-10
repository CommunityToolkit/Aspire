using Aspire.CommunityToolkit.Testing;
using FluentAssertions;

namespace Aspire.CommunityToolkit.Hosting.Golang.Tests;

#pragma warning disable CTASPIRE001
public class AppHostTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Golang_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Golang_AppHost>>
{
    [Theory]
    [InlineData("golang")]
    public async Task ResourceStartsAndRespondsOk(string appName)
    {
        var httpClient = fixture.CreateHttpClient(appName);

        await fixture.App.WaitForTextAsync("Listening and serving HTTP on :8080", appName).WaitAsync(TimeSpan.FromMinutes(2));

        var response = await httpClient.GetAsync("/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}