using Aspire.CommunityToolkit.Testing;
using FluentAssertions;
using Microsoft.TestUtilities;

namespace Aspire.CommunityToolkit.Hosting.Java.Tests;

#pragma warning disable CTASPIRE001
public class JavaHostingComponentTests(AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Java_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.Aspire_CommunityToolkit_Hosting_Java_AppHost>>
{
    [ConditionalFact]
    [OSSkipCondition(OperatingSystems.Windows)]
    public async Task ContainerAppResourceWillRespondWithOk()
    {
        var resourceName = "containerapp";
        var httpClient = fixture.CreateHttpClient(resourceName);

        await fixture.App.WaitForTextAsync("Started SpringMavenApplication", resourceName).WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [ConditionalFact]
    [OSSkipCondition(OperatingSystems.Windows)]
    public async Task ExecutableAppResourceWillRespondWithOk()
    {
        var resourceName = "executableapp";
        var httpClient = fixture.CreateHttpClient(resourceName);

        await fixture.App.WaitForTextAsync("Started SpringMavenApplication", resourceName).WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}