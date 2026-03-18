using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Perl.Tests;

/// <summary>
/// Shared helper for logging behavior tests. Fires <see cref="BeforeStartEvent"/>
/// on the builder and returns log lines collected from the resource via
/// <see cref="ResourceLoggerService"/>.
/// </summary>
internal static class LoggingTestHelper
{
    /// <summary>
    /// Uses <see cref="EventDispatchBehavior.BlockingConcurrent"/> so that all
    /// handlers execute even when Aspire infrastructure handlers (DCP, etc.)
    /// throw in unit test environments without full tooling configured.
    /// </summary>
    internal static async Task<List<string>> PublishBeforeStartAndCollectLogsAsync(
        IDistributedApplicationBuilder builder,
        DistributedApplication app,
        string resourceName,
        TimeSpan? timeout = null)
    {
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var eventing = (DistributedApplicationEventing)builder.Eventing;

        try
        {
            await eventing.PublishAsync(
                new BeforeStartEvent(app.Services, appModel),
                EventDispatchBehavior.BlockingConcurrent,
                CancellationToken.None);
        }
        catch (Exception)
        {
            // Expected: Aspire DCP infrastructure handlers throw in unit test
            // environments that lack the Aspire CLI / dashboard binaries.
        }

        var loggerService = app.Services.GetRequiredService<ResourceLoggerService>();
        var logs = new List<string>();
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(3));
        try
        {
            await foreach (var batch in loggerService.WatchAsync(resourceName).WithCancellation(cts.Token))
            {
                logs.AddRange(batch.Select(l => l.Content));
                break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when no logs are produced (e.g. publish mode).
        }

        return logs;
    }
}
