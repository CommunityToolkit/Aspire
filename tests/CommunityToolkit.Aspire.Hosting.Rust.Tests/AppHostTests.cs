using CommunityToolkit.Aspire.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.Rust.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Rust_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Rust_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var appName = "rust-app";
        var logger = fixture.App.Services.GetRequiredService<ILogger<AppHostTests>>();

        logger.LogInformation($"[Custom]: Waiting for resource {appName} to be healthy...");

        var rns = fixture.App.Services.GetRequiredService<ResourceNotificationService>();
        var re = await rns.WaitForResourceHealthyAsync(appName).WaitAsync(TimeSpan.FromMinutes(10));

        logger.LogInformation($"[Custom]: Resource {appName} is healthy!");
        logger.LogInformation($"[Custom]: Resource event: {JsonSerializer.Serialize(re, new JsonSerializerOptions { WriteIndented = true })}");
        logger.LogInformation("[Custom]: Pinging the resource...");

        var httpClient = fixture.CreateHttpClient(appName);
        var response = await httpClient.GetAsync("/ping");

        logger.LogInformation($"[Custom]: Response: {response.StatusCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
