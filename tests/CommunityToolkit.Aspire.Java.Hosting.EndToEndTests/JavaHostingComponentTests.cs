using System.Net;
using CommunityToolkit.Aspire.Testing;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Java.Hosting.EndToEndTests;

[Collection("Integration Tests")]
public class JavaHostingComponentTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Java_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Java_AppHost>>
{
    [Fact]
    public async Task Given_Container_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        var httpClient = fixture.App.CreateHttpClient("containerapp");

        await fixture.ResourceNotificationService.WaitForResourceAsync("containerapp", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Given_Executable_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        var httpClient = fixture.App.CreateHttpClient("executableapp");

        await fixture.ResourceNotificationService.WaitForResourceAsync("executableapp", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}