using Aspire.CommunityToolkit.Testing;
using FluentAssertions;
using Microsoft.TestUtilities;

namespace Aspire.CommunityToolkit.Hosting.DataApiBuilder.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_DataApiBuilder_AppHost>>
{
    [ConditionalFact]
    [OSSkipCondition(OperatingSystems.Windows)]
    public async Task ResourceStartsAndRespondsOk()
    {
        await fixture.ResourceNotificationService.WaitForResourceAsync("dab", KnownResourceStates.Running).WaitAsync(TimeSpan.FromMinutes(5));
        var httpClient = fixture.CreateHttpClient("dab");

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}