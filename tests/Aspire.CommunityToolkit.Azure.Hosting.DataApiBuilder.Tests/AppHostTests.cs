using Aspire.CommunityToolkit.Testing;
using Aspire.Components.Common.Tests;
using FluentAssertions;

namespace Aspire.CommunityToolkit.Hosting.DataApiBuilder.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost>>
{
    [RequiresDocker]
    public async Task ResourceStartsAndRespondsOk()
    {
        await fixture.ResourceNotificationService.WaitForResourceAsync("dab", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(1));
        var httpClient = fixture.CreateHttpClient("dab", "http");

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}