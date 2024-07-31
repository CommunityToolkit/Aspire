using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Java.Hosting.EndToEndTests;

public class ProgramTests(ITestOutputHelper testOutput)
{
    [Fact]
    public async Task Given_Container_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CommunityToolkit_Aspire_Java_AppHost>();

        appHost.Services
            .AddLogging(builder =>
            {
                builder.AddXUnit(testOutput);
                builder.SetMinimumLevel(LogLevel.Trace);
            })
            // .ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler())
            ;

        await using var app = await appHost.BuildAsync();
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        await app.StartAsync();

        // Act
        var httpClient = app.CreateHttpClient("containerapp");

        await resourceNotificationService.WaitForResourceAsync("containerapp", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Given_Executable_Resource_When_Invoked_Then_Root_Returns_OK()
    {
        // Arrange
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CommunityToolkit_Aspire_Java_AppHost>();

        appHost.Services
            .AddLogging(builder =>
            {
                builder.AddXUnit(testOutput);
                builder.SetMinimumLevel(LogLevel.Trace);
            })
            // .ConfigureHttpClientDefaults(clientBuilder => clientBuilder.AddStandardResilienceHandler())
            ;

        await using var app = await appHost.BuildAsync();
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        await app.StartAsync();

        // Act
        var httpClient = app.CreateHttpClient("executableapp");

        await resourceNotificationService.WaitForResourceAsync("executableapp", KnownResourceStates.Running).WaitAsync(TimeSpan.FromSeconds(30));

        var response = await httpClient.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}