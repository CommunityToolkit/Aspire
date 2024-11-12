using CommunityToolkit.Aspire.Testing;
using FluentAssertions;

namespace CommunityToolkit.Aspire.Hosting.Rust.Tests;

public class AppHostTests(AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Rust_AppHost> fixture) : IClassFixture<AspireIntegrationTestFixture<Projects.CommunityToolkit_Aspire_Hosting_Rust_AppHost>>
{
    [Fact]
    public async Task ResourceStartsAndRespondsOk()
    {
        var appName = "rust-app";

        var rns = fixture.App.Services.GetRequiredService<ResourceNotificationService>();
        _ = await rns.WaitForResourceHealthyAsync(appName).WaitAsync(TimeSpan.FromMinutes(5));

        var httpClient = fixture.CreateHttpClient(appName);
        var response = await httpClient.GetAsync("/ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
