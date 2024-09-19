using CommunityToolkit.Aspire.Testing;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Hosting.Ollama.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Ollama_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Ollama_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var httpClient = fixture.CreateHttpClient("ollama");

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}