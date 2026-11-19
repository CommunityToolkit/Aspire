using CommunityToolkit.Aspire.Testing;

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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public class BaconAppHostTests
    {
        [Fact]
        public async Task BaconStartsAndServesRequests()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(5));
            var cancellationToken = timeout.Token;

            foreach (var command in new[] { "cargo", "rustc", "bacon" })
            {
                await ProcessTestUtilities.RunProcessAsync(command, ["--version"], Directory.GetCurrentDirectory(), cancellationToken);
            }

            await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.CommunityToolkit_Aspire_Hosting_Rust_AppHost>(
                ["--ENABLE_BACON=1"], cancellationToken);
            await using var app = await builder.BuildAsync(cancellationToken);
            await app.StartAsync(cancellationToken);

            const string appName = "bacon-app";
            var notifications = app.Services.GetRequiredService<ResourceNotificationService>();
            await notifications.WaitForResourceHealthyAsync(appName, cancellationToken);

            using var client = app.CreateHttpClient(appName);
            Assert.Equal("pong", await client.GetStringAsync("/ping", cancellationToken));
        }
    }
}
