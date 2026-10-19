using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.Utils;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Loki.Tests;

[RequiresDocker]
public class LokiFunctionalTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DataMountPersistsLogsBetweenContainers(bool useVolume)
    {
        var testId = Guid.NewGuid().ToString("N");
        var volumeName = useVolume ? $"loki-test-{testId}" : null;
        var bindMountPath = useVolume ? null : Directory.CreateTempSubdirectory("loki-test-").FullName;
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
            for (var usage = 0; usage < 2; usage++)
            {
                using var builder = TestDistributedApplicationBuilder.Create(testOutputHelper);
                var loki = builder.AddLoki("loki");
                if (useVolume)
                {
                    loki.WithDataVolume(volumeName);
                }
                else
                {
                    loki.WithDataBindMount(bindMountPath!);
                }

                using var app = builder.Build();
                await app.StartAsync(cancellationToken);
                try
                {
                    await app.Services.GetRequiredService<ResourceNotificationService>()
                        .WaitForResourceHealthyAsync("loki", cancellationToken);
                    using var client = new HttpClient { BaseAddress = new Uri(loki.Resource.HttpEndpoint.Url) };
                    if (usage == 0)
                    {
                        var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000).ToString(CultureInfo.InvariantCulture);
                        using var push = await client.PostAsJsonAsync("/loki/api/v1/push", new
                        {
                            streams = new[]
                            {
                                new
                                {
                                    stream = new { test_id = testId },
                                    values = new[] { new[] { timestamp, "persisted log" } }
                                }
                            }
                        }, cancellationToken);
                        Assert.Equal(HttpStatusCode.NoContent, push.StatusCode);
                        using var flush = await client.PostAsync("/flush?wait=true", null, cancellationToken);
                        flush.EnsureSuccessStatusCode();
                    }

                    var query = Uri.EscapeDataString($"{{test_id=\"{testId}\"}}");
                    using var response = await client.GetAsync($"/loki/api/v1/query_range?query={query}", cancellationToken);
                    response.EnsureSuccessStatusCode();
                    using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                    var stream = Assert.Single(result.RootElement.GetProperty("data").GetProperty("result").EnumerateArray());
                    var value = Assert.Single(stream.GetProperty("values").EnumerateArray());
                    Assert.Equal("persisted log", value[1].GetString());
                }
                finally
                {
                    await app.StopAsync(CancellationToken.None);
                }
            }
        }
        finally
        {
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
}