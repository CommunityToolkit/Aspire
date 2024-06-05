using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using OllamaSharp;

namespace Raygun.Aspire.Hosting.Ollama
{
  internal class OllamaResourceLifecycleHook : IDistributedApplicationLifecycleHook, IAsyncDisposable
  {
    private readonly ResourceNotificationService _notificationService;

    private readonly CancellationTokenSource _tokenSource = new();

    public OllamaResourceLifecycleHook(ResourceNotificationService notificationService)
    {
      _notificationService = notificationService;
    }

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
      if (string.IsNullOrWhiteSpace(resource.InitialModel))
      {
        return;
      }

      _ = Task.Run(async () =>
      {
        try
        {
          var ollamaClient = new OllamaApiClient(new Uri(resource.ConnectionStringExpression.ValueExpression));
          var model = resource.InitialModel;

          await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot("Checking model", KnownResourceStateStyles.Info) });
          var hasModel = await HasModelAsync(ollamaClient, model, cancellationToken);

          if (!hasModel)
          {
            await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot("Downloading model", KnownResourceStateStyles.Info) });

            long percentage = 0;

            await ollamaClient.PullModel(model, async status =>
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
            }, cancellationToken);
          }

          await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot("Ready", KnownResourceStateStyles.Success) });
        }
        catch (Exception ex)
        {
          await _notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot(ex.Message, KnownResourceStateStyles.Error) });
        }

      }, cancellationToken);
    }

    private async Task<bool> HasModelAsync(OllamaApiClient ollamaClient, string model, CancellationToken cancellationToken)
    {
      var localModels = await ollamaClient.ListLocalModels(cancellationToken);
      return localModels.Any(m => m.Name.StartsWith(model));
    }

    public ValueTask DisposeAsync()
    {
      _tokenSource.Cancel();
      return default;
    }
  }
}
