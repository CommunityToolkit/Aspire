using System.Net;
using CommunityToolkit.Aspire.Testing;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Java.Hosting.EndToEndTests;

#pragma warning disable CTASPIRESRC001
public class JavaHostingComponentTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Java_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Java_AppHost>>
{
    [Theory]
    [InlineData("containerapp")]
    [InlineData("executableapp")]
    public async Task ResourceWillRespondWithOk(string resourceName)
    {
        var httpClient = fixture.CreateHttpClient(resourceName);

        await fixture.App.WaitForTextAsync("Started SpringMavenApplication", resourceName).WaitAsync(TimeSpan.FromMinutes(5));

        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}