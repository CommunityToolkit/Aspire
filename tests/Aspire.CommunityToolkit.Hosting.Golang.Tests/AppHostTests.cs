using Aspire.CommunityToolkit.Testing;
using FluentAssertions;
using Microsoft.TestUtilities;

namespace Aspire.CommunityToolkit.Hosting.Golang.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Golang_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Golang_AppHost>>
{
    [ConditionalFact]
    public async Task ResourceStartsAndRespondsOk()
    {
        await fixture.ResourceNotificationService.WaitForResourceAsync("golang", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient("golang");

        await Task.Delay(30000);

        var response = await httpClient.GetAsync("/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}