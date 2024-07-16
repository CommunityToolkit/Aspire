using System.Net;

using FluentAssertions;

namespace CommunityToolkit.Aspire.AppHost.Tests;

public class ProgramTests
{
    [Fact(Skip = "Unable to run on CI. Waiting to find out how to get better diagnostics.")]
    public async Task Given_Container_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CommunityToolkit_Aspire_Java_AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        // Act
        var httpClient = app.CreateHttpClient("containerapp");

        await Task.Delay(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Skip = "Unable to run on CI. Waiting to find out how to get better diagnostics.")]
    public async Task Given_Executable_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CommunityToolkit_Aspire_Java_AppHost>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        // Act
        var httpClient = app.CreateHttpClient("executableapp");

        await Task.Delay(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}