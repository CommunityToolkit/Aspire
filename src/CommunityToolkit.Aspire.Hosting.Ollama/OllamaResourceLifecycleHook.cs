using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using System.Globalization;

namespace Aspire.CommunityToolkit.Hosting.Ollama;
internal class OllamaResourceLifecycleHook(
    ResourceLoggerService loggerService,
    ResourceNotificationService notificationService,
    DistributedApplicationExecutionContext context) : IDistributedApplicationLifecycleHook, IAsyncDisposable
{
    private readonly ResourceNotificationService _notificationService = notificationService;

    private readonly CancellationTokenSource _tokenSource = new();

    public Task AfterResourcesCreatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        if (context.IsPublishMode)
        {
            return Task.CompletedTask;
        }

        foreach (var resource in appModel.Resources.OfType<OllamaResource>())
        {
            DownloadModel(resource, _tokenSource.Token);
        }

        return Task.CompletedTask;
    }

    private void DownloadModel(OllamaResource resource, CancellationToken cancellationToken)
    {
        var logger = loggerService.GetLogger(resource);

        _ = Task.Run(async () =>
        {
            foreach (string model in resource.Models)
            {
                try
                {
                    var connectionString = await resource.ConnectionStringExpression.GetValueAsync(cancellationToken).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(connectionString))
                    {
                        await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot("No connection string", KnownResourceStateStyles.Error) });
                        return;
                    }

                    var ollamaClient = new OllamaApiClient(new Uri(connectionString));

                    await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot($"Checking {model}", KnownResourceStateStyles.Info) });
                    var hasModel = await HasModelAsync(ollamaClient, model, cancellationToken);

                    if (!hasModel)
                    {
                        logger.LogInformation("{TimeStamp}: [{Model}] needs to be downloaded for {ResourceName}",
                            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                            model,
                            resource.Name);
                        await PullModel(resource, ollamaClient, model, logger, cancellationToken);
                    }
                    else
                    {
                        logger.LogInformation("{TimeStamp}: [{Model}] already exists for {ResourceName}",
                            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                            model,
                            resource.Name);
                    }

                    await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot("Running", KnownResourceStateStyles.Success) });
                }
                catch (Exception ex)
                {
                    await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot(ex.Message, KnownResourceStateStyles.Error) });
                    break;
                }
            }

        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> HasModelAsync(OllamaApiClient ollamaClient, string model, CancellationToken cancellationToken)
    {
        int retryCount = 0;
        while (retryCount < 5)
        {
            try
            {
                var localModels = await ollamaClient.ListLocalModels(cancellationToken);
                return localModels.Any(m => m.Name.StartsWith(model));
            }
            catch (TaskCanceledException)
            {
                // wait 30 seconds before retrying
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                retryCount++;
            }
        }

        throw new TimeoutException("Failed to list local models after 5 retries. Likely that the container image was not pulled in time, or the container is not running.");
    }

    private async Task PullModel(OllamaResource resource, OllamaApiClient ollamaClient, string model, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("{TimeStamp}: Pulling ollama model {Model}...",
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            model);
        await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot($"Downloading {model}", KnownResourceStateStyles.Info) });

        long percentage = 0;

        await foreach (PullModelResponse? status in ollamaClient.PullModel(model, cancellationToken))
        {
            if (status is null)
            {
                continue;
            }

            if (status.Total != 0)
            {
                var newPercentage = (long)(status.Completed / (double)status.Total * 100);
                if (newPercentage != percentage)
                {
                    percentage = newPercentage;

                    var percentageState = $"Downloading {model}{(percentage > 0 ? $" {percentage} percent" : "")}";
                    await _notificationService.PublishUpdateAsync(resource,
                    state => state with
                    {
                        State = new ResourceStateSnapshot(percentageState, KnownResourceStateStyles.Info)
                    });
                }
            }
        }

        logger.LogInformation("{TimeStamp}: Finished pulling ollama model {Model}",
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            model);
    }

    public ValueTask DisposeAsync()
    {
        _tokenSource.Cancel();
        return default;
    }
}
