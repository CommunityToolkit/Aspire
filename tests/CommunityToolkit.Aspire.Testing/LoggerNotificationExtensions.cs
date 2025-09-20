// copied from: https://github.com/dotnet/aspire/blob/2ea981718d2addc835b033fc4a52fae63b2a4a51/tests/Aspire.Hosting.Tests/Utils/LoggerNotificationExtensions.cs
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace CommunityToolkit.Aspire.Testing;

public static class LoggerNotificationExtensions
{
    /// <summary>
    /// Waits for the specified text to be logged.
    /// </summary>
    /// <param name="app">The <see cref="DistributedApplication" /> instance to watch.</param>
    /// <param name="logText">The text to wait for.</param>
    /// <param name="resourceName">An optional resource name to filter the logs for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <remarks>
    /// This has been copied from the Aspire.Hosting.Tests project and will likely be removed in the future.
    /// </remarks>
    [Experimental("CTASPIRE001", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
    public static Task WaitForTextAsync(this DistributedApplication app, string logText, string? resourceName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrEmpty(logText);

        return WaitForTextAsync(app, (log) => log.Contains(logText), resourceName, cancellationToken);
    }


    /// <summary>
    /// Waits for the specified text to be logged.
    /// </summary>
    /// <param name="app">The <see cref="DistributedApplication" /> instance to watch.</param>
    /// <param name="predicate">A predicate checking the text to wait for.</param>
    /// <param name="resourceName">An optional resource name to filter the logs for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    /// <remarks>
    /// This has been copied from the Aspire.Hosting.Tests project and will likely be removed in the future.
    /// </remarks>
    [Experimental("CTASPIRE001", UrlFormat = "https://aka.ms/communitytoolkit/aspire/diagnostics#{0}")]
    public static Task WaitForTextAsync(this DistributedApplication app, Predicate<string> predicate, string? resourceName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(predicate);

        var hostApplicationLifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

        var watchCts = CancellationTokenSource.CreateLinkedTokenSource(hostApplicationLifetime.ApplicationStopping, cancellationToken);

        var tcs = new TaskCompletionSource();

        _ = Task.Run(() => WatchNotifications(app, resourceName, predicate, tcs, watchCts), watchCts.Token);

        return tcs.Task;
    }


    private static async Task WatchNotifications(DistributedApplication app, string? resourceName, Predicate<string> predicate, TaskCompletionSource tcs, CancellationTokenSource cancellationTokenSource)
    {
        var resourceNotificationService = app.Services.GetRequiredService<ResourceNotificationService>();
        var resourceLoggerService = app.Services.GetRequiredService<ResourceLoggerService>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(LoggerNotificationExtensions));

        var loggingResourceIds = new HashSet<string>();
        var logWatchTasks = new List<Task>();

        try
        {
            await foreach (var resourceEvent in resourceNotificationService.WatchAsync(cancellationTokenSource.Token).ConfigureAwait(false))
            {
                if (resourceName != null && !string.Equals(resourceEvent.Resource.Name, resourceName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var resourceId = resourceEvent.ResourceId;

                if (loggingResourceIds.Add(resourceId))
                {
                    // Start watching the logs for this resource ID
                    logWatchTasks.Add(WatchResourceLogs(tcs, resourceId, predicate, resourceLoggerService, cancellationTokenSource));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if the application stops prematurely or the text was detected.
            tcs.TrySetCanceled();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while watching for resource notifications.");
            tcs.TrySetException(ex);
        }
    }

    private static async Task WatchResourceLogs(TaskCompletionSource tcs, string resourceId, Predicate<string> predicate, ResourceLoggerService resourceLoggerService, CancellationTokenSource cancellationTokenSource)
    {
        await foreach (var logEvent in resourceLoggerService.WatchAsync(resourceId).WithCancellation(cancellationTokenSource.Token).ConfigureAwait(false))
        {
            foreach (var line in logEvent)
            {
                if (predicate(line.Content))
                {
                    tcs.SetResult();
                    cancellationTokenSource.Cancel();
                    return;
                }
            }
        }
    }
}
