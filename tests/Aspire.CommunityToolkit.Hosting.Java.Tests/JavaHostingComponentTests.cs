using Aspire.CommunityToolkit.Testing;
using FluentAssertions;
using Microsoft.TestUtilities;

namespace Aspire.CommunityToolkit.Hosting.Java.Tests;

#pragma warning disable CTASPIRE001
public class JavaHostingComponentTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Java_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Java_AppHost>>
{
    [ConditionalTheory]
    [OSSkipCondition(OperatingSystems.Windows)]
    [InlineData("containerapp")]
    [InlineData("executableapp")]
    public async Task AppResourceWillRespondWithOk(string resourceName)
    {
        var httpClient = fixture.CreateHttpClient(resourceName);

        await fixture.App.WaitForTextAsync("Started SpringMavenApplication", resourceName).WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}