using System.Net;
using CommunityToolkit.Aspire.Testing;
using FluentAssertions;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Java.Hosting.EndToEndTests;

[Collection("Integration Tests")]
public class JavaHostingComponentTests(ITestOutputHelper testOutput) : AspireIntegrationTest<Projects.CommunityToolkit_Aspire_Java_AppHost>(testOutput)
{
    [Fact]
    public async Task Given_Container_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        var httpClient = app.CreateHttpClient("containerapp");

        await ResourceNotificationService.WaitForResourceAsync("containerapp", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Given_Executable_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        var httpClient = app.CreateHttpClient("executableapp");

        await ResourceNotificationService.WaitForResourceAsync("executableapp", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}