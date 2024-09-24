using Aspire.CommunityToolkit.Testing;
using FluentAssertions;

namespace Aspire.CommunityToolkit.Hosting.Ollama.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Ollama_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Ollama_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        await fixture.ResourceNotificationService.WaitForResourceAsync("ollama", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient("ollama", "ollama");

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}