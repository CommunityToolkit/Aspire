using CommunityToolkit.Aspire.Testing;
using FluentAssertions;
using System.Text.Json;
using Xunit.Abstractions;

namespace CommunityToolkit.Aspire.Hosting.Rust.Tests;

public class AppHostTests(
    AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Rust_AppHost> fixture, ITestOutputHelper outputHelper) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Rust_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var appName = "rust-app";
        var httpClient = fixture.CreateHttpClient(appName);

        outputHelper.WriteLine($"[Custom]: Waiting for resource {appName} to be healthy...");

        var rns = fixture.App.Services.GetRequiredService<ResourceNotificationService>();
        var re = await rns.WaitForResourceHealthyAsync(appName).WaitAsync(TimeSpan.FromMinutes(10));

        outputHelper.WriteLine($"[Custom]: Resource {appName} is healthy!");
        outputHelper.WriteLine($"[Custom]: Resource event: {JsonSerializer.Serialize(re, new JsonSerializerOptions { WriteIndented = true })}");
        outputHelper.WriteLine("[Custom]: Pinging the resource...");

        var response = await httpClient.GetAsync("/ping");

        outputHelper.WriteLine($"[Custom]: Response: {response.StatusCode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
