using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Tempo.Tests;

[RequiresDocker]
public class TempoFunctionalTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task ResourceStartsAndStoresTraces()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var cancellationToken = cancellation.Token;
        using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
        var tempo = builder.AddTempo("tempo");
        using var app = builder.Build();
        await app.StartAsync(cancellationToken);
        try
        {
            await app.Services.GetRequiredService<ResourceNotificationService>()
                .WaitForResourceHealthyAsync("tempo", cancellationToken);
            var traceId = Guid.NewGuid().ToString("N");
            await SendTraceAsync(tempo.Resource, traceId, cancellationToken);
            await AssertTraceAsync(tempo.Resource, traceId, cancellationToken);
        }
        finally
        {
            await app.StopAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DataMountPersistsTracesBetweenContainers(bool useVolume)
    {
        var traceId = Guid.NewGuid().ToString("N");
        var volumeName = useVolume ? $"tempo-test-{traceId}" : null;
        var bindMountPath = useVolume ? null : Directory.CreateTempSubdirectory("tempo-test-").FullName;
        var configPath = Path.GetTempFileName();
        if (bindMountPath is not null && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(bindMountPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        }

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var cancellationToken = cancellation.Token;
        try
        {
            await File.WriteAllTextAsync(configPath, """
                server:
                  http_listen_port: 3200
                  grpc_listen_port: 9095
                distributor:
                  receivers:
                    otlp:
                      protocols:
                        grpc:
                          endpoint: 0.0.0.0:4317
                        http:
                          endpoint: 0.0.0.0:4318
                ingester:
                  flush_all_on_shutdown: true
                storage:
                  trace:
                    backend: local
                    wal:
                                            path: /var/tempo/wal
                    local:
                                            path: /var/tempo/blocks
                """, cancellationToken);

            for (var usage = 0; usage < 2; usage++)
            {
                using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
                var tempo = builder.AddTempo("tempo").WithConfigFile(configPath);
                if (useVolume)
                {
                    tempo.WithDataVolume(volumeName);
                }
                else
                {
                    tempo.WithDataBindMount(bindMountPath!);
                }

                using var app = builder.Build();
                await app.StartAsync(cancellationToken);
                try
                {
                    await app.Services.GetRequiredService<ResourceNotificationService>()
                        .WaitForResourceHealthyAsync("tempo", cancellationToken);
                    if (usage == 0)
                    {
                        await SendTraceAsync(tempo.Resource, traceId, cancellationToken);
                    }
                    await AssertTraceAsync(tempo.Resource, traceId, cancellationToken);
                }
                finally
                {
                    await app.StopAsync(CancellationToken.None);
                }
            }
        }
        finally
        {
            File.Delete(configPath);
            if (volumeName is not null)
            {
                DockerUtils.AttemptDeleteDockerVolume(volumeName);
            }
            if (bindMountPath is not null)
            {
                try
                {
                    Directory.Delete(bindMountPath, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    testOutputHelper.WriteLine($"Could not clean up bind mount '{bindMountPath}': {exception.Message}");
                }
            }
        }
    }

    private static async Task SendTraceAsync(TempoResource tempo, string traceId, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri(tempo.OtlpHttpEndpoint.Url) };
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000;
        using var response = await client.PostAsJsonAsync("/v1/traces", new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new
                    {
                        attributes = new[] { new { key = "service.name", value = new { stringValue = "tempo-test" } } }
                    },
                    scopeSpans = new[]
                    {
                        new
                        {
                            scope = new { name = "tempo-test" },
                            spans = new[]
                            {
                                new
                                {
                                    traceId,
                                    spanId = "0123456789abcdef",
                                    name = "Tempo integration test",
                                    kind = 2,
                                    startTimeUnixNano = timestamp.ToString(CultureInfo.InvariantCulture),
                                    endTimeUnixNano = (timestamp + 1_000_000).ToString(CultureInfo.InvariantCulture)
                                }
                            }
                        }
                    }
                }
            }
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task AssertTraceAsync(TempoResource tempo, string traceId, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri(tempo.HttpEndpoint.Url) };
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        using var ready = await client.GetAsync("/ready", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        using var response = await client.GetAsync($"/api/traces/{traceId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var trace = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var spans = trace.RootElement.GetProperty("batches").EnumerateArray()
            .SelectMany(batch => batch.GetProperty("scopeSpans").EnumerateArray())
            .SelectMany(scope => scope.GetProperty("spans").EnumerateArray());
        Assert.Contains(spans, span => span.GetProperty("name").GetString() == "Tempo integration test");
    }
}