using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using System.Data.Common;
using System.Globalization;

namespace CommunityToolkit.Aspire.Hosting.Ollama;

internal class OllamaModelResourceLifecycleHook(
    ResourceLoggerService loggerService,
    ResourceNotificationService notificationService,
    DistributedApplicationExecutionContext context) : IDistributedApplicationLifecycleHook, IAsyncDisposable
{
    public const string ModelAvailableState = "ModelAvailable";

    private readonly CancellationTokenSource _tokenSource = new();

    public async Task AfterResourcesCreatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        if (context.IsPublishMode)
        {
            return;
        }

        await Parallel.ForEachAsync(appModel.Resources.OfType<OllamaModelResource>(), _tokenSource.Token, async (resource, ct) =>
        {
            await DownloadModel(resource, ct);
        });
    }

    private async Task DownloadModel(OllamaModelResource modelResource, CancellationToken cancellationToken)
    {
        var logger = loggerService.GetLogger(modelResource);
        string model = modelResource.ModelName;
        var ollamaResource = modelResource.Parent;

        try
        {
            var connectionString = await ollamaResource.ConnectionStringExpression.GetValueAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot("No connection string", KnownResourceStateStyles.Error) });
                return;
            }

            if (!Uri.TryCreate(connectionString, UriKind.Absolute, out _))
            {
                var connectionBuilder = new DbConnectionStringBuilder
                {
                    ConnectionString = connectionString
                };

                if (connectionBuilder.ContainsKey("Endpoint") && Uri.TryCreate(connectionBuilder["Endpoint"].ToString(), UriKind.Absolute, out var endpoint))
                {
                    connectionString = endpoint.ToString();
                }
            }

            var ollamaClient = new OllamaApiClient(new Uri(connectionString));

            await notificationService.PublishUpdateAsync(modelResource, state => state with
            {
                State = new ResourceStateSnapshot($"Checking {model}", KnownResourceStateStyles.Info),
                Properties = [.. state.Properties, new(CustomResourceKnownProperties.Source, model)]
            });

            var hasModel = await HasModelAsync(ollamaClient, model, cancellationToken);

            if (!hasModel)
            {
                logger.LogInformation("{TimeStamp}: [{Model}] needs to be downloaded for {ResourceName}",
                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                    model,
                    ollamaResource.Name);
                await OllamaUtilities.PullModelAsync(modelResource, ollamaClient, model, logger, notificationService, cancellationToken);
            }
            else
            {
                logger.LogInformation("{TimeStamp}: [{Model}] already exists for {ResourceName}",
                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                    model,
                    ollamaResource.Name);
            }

            await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot(ModelAvailableState, KnownResourceStateStyles.Success) });
        }
        catch (Exception ex)
        {
            await notificationService.PublishUpdateAsync(modelResource, state => state with { State = new ResourceStateSnapshot(ex.Message, KnownResourceStateStyles.Error) });
        }
    }

    private static async Task<bool> HasModelAsync(OllamaApiClient ollamaClient, string model, CancellationToken cancellationToken)
    {
        int retryCount = 0;
        while (retryCount < 5)
        {
            try
            {
                var localModels = await ollamaClient.ListLocalModelsAsync(cancellationToken);
                return localModels.Any(m => m.Name.Equals(model, StringComparison.OrdinalIgnoreCase));
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

    public ValueTask DisposeAsync()
    {
        _tokenSource.Cancel();
        return default;
    }
}
