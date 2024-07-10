using System.Net;

using FluentAssertions;

namespace Aspire.Contribs.Hosting.Java.Tests;

public class JavaAppHostingExtensionTests
{
    [Fact]
    public async Task Given_Container_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Aspire_Contribs_Hosting_Java_Tests>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        Thread.Sleep(5000);

        // Act
        var httpClient = app.CreateHttpClient("containerapp");
        var response = await httpClient.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Given_Executable_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Aspire_Contribs_Hosting_Java_Tests>();
        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        Thread.Sleep(10000);

        // Act
        var httpClient = app.CreateHttpClient("executableapp");
        var response = await httpClient.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
