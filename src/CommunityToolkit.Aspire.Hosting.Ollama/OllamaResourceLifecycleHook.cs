using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using OllamaSharp;
using OllamaSharp.Models;

namespace CommunityToolkit.Aspire.Hosting.Ollama;
internal class OllamaResourceLifecycleHook(ResourceNotificationService notificationService) : IDistributedApplicationLifecycleHook, IAsyncDisposable
{
    private readonly ResourceNotificationService _notificationService = notificationService;

    private readonly CancellationTokenSource _tokenSource = new();

    public Task AfterResourcesCreatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        foreach (var resource in appModel.Resources.OfType<OllamaResource>())
        {
            DownloadModel(resource, _tokenSource.Token);
        }

        return Task.CompletedTask;
    }

    private void DownloadModel(OllamaResource resource, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resource.ModelName))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var connectionString = await resource.ConnectionStringExpression.GetValueAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot("No connection string", KnownResourceStateStyles.Error) });
                    return;
                }

                var ollamaClient = new OllamaApiClient(new Uri(connectionString));
                var model = resource.ModelName;

                await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot("Checking model", KnownResourceStateStyles.Info) });
                var hasModel = await HasModelAsync(ollamaClient, model, cancellationToken);

                if (!hasModel)
                {
                    await PullModel(resource, ollamaClient, model, cancellationToken);
                }

                await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot("Running", KnownResourceStateStyles.Success) });
            }
            catch (Exception ex)
            {
                await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot(ex.Message, KnownResourceStateStyles.Error) });
            }

        }, cancellationToken);
    }

    private static async Task<bool> HasModelAsync(OllamaApiClient ollamaClient, string model, CancellationToken cancellationToken)
    {
        var localModels = await ollamaClient.ListLocalModels(cancellationToken);
        return localModels.Any(m => m.Name.StartsWith(model));
    }

    private async Task PullModel(OllamaResource resource, OllamaApiClient ollamaClient, string model, CancellationToken cancellationToken)
    {
        await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot("Downloading model", KnownResourceStateStyles.Info) });

        long percentage = 0;

        await foreach (PullModelResponse? status in ollamaClient.PullModel(model, cancellationToken))
        {
            if (status == null)
            {
                continue;
            }

            if (status.Total != 0)
            {
                var newPercentage = (long)(status.Completed / (double)status.Total * 100);
                if (newPercentage != percentage)
                {
                    percentage = newPercentage;

                    var percentageState = percentage == 0 ? "Downloading model" : $"Downloading model {percentage} percent";
                    await _notificationService.PublishUpdateAsync(resource,
                    state => state with
                    {
                        State = new ResourceStateSnapshot(percentageState, KnownResourceStateStyles.Info)
                    });
                }
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _tokenSource.Cancel();
        return default;
    }
}
