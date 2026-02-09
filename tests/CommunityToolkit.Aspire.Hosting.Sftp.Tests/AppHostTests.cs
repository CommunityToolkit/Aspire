using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;
using Polly;
using Projects;
using Renci.SshNet;

namespace CommunityToolkit.Aspire.Hosting.Sftp.Tests;

[RequiresDocker]
public class AppHostTests(ITestOutputHelper log, AspireIntegrationTestFixture<CommunityToolkit_Aspire_Hosting_Sftp_AppHost> fix)
    : IClassFixture<AspireIntegrationTestFixture<CommunityToolkit_Aspire_Hosting_Sftp_AppHost>>
{
    [Fact]
    public async Task ApiUploadsAndDownloadsTestFile()
    {
        await fix.ResourceNotificationService.WaitForResourceHealthyAsync("api");

        using var client = fix.CreateHttpClient("api");

        using var healthRequest = new HttpRequestMessage(HttpMethod.Get, "health");
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "upload");
        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, "download");

        await RunAsync(healthRequest);
        await RunAsync(uploadRequest);
        await RunAsync(downloadRequest);

        async Task RunAsync(HttpRequestMessage req)
        {
            var res = await client.SendAsync(req);

            Assert.True(res.IsSuccessStatusCode);
        }
    }

    [Fact]
    public async Task ResourcesStartAndClientConnects()
    {
        await fix.ResourceNotificationService.WaitForResourceHealthyAsync("sftp");

        var connectionString = await fix.GetConnectionString("sftp");

        var uri = new Uri(connectionString!);

        using var client = new SftpClient(uri.Host, uri.Port, "foo", "pass");

        try
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(5, _ => TimeSpan.FromMilliseconds(1000));

            await retryPolicy.ExecuteAsync(async () =>
            {
                log.WriteLine($"Connecting to resource 'sftp' using connection string: {connectionString}");

                using var connectTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));

                await client.ConnectAsync(connectTokenSource.Token);
            });

            Assert.True(client.IsConnected);

            Assert.NotNull(client.ConnectionInfo);
            Assert.Equal(uri.Host, client.ConnectionInfo.Host);
            Assert.Equal(uri.Port, client.ConnectionInfo.Port);
            Assert.True(client.ConnectionInfo.IsAuthenticated);
        }
        finally
        {
            client.Disconnect();
        }
    }
}
