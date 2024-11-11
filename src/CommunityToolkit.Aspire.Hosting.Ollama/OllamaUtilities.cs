using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OllamaSharp.Models;
using System.Data.Common;
using System.Globalization;

namespace CommunityToolkit.Aspire.Hosting.Ollama;

internal static class OllamaUtilities
{
    public static async Task<(bool hasConnectionString, Uri? endpoint)> TryGetEndpointAsync(this IResourceWithConnectionString resource, CancellationToken ct)
    {
        var connectionString = await resource.ConnectionStringExpression.GetValueAsync(ct);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (false, null);
        }

        var connectionBuilder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        if (!Uri.TryCreate((string)connectionBuilder["Endpoint"], UriKind.Absolute, out var endpoint))
        {
            return (false, null);
        }

        return (true, endpoint);
    }

    
    public static async Task PullModelAsync(OllamaModelResource resource, IOllamaApiClient ollamaClient, string model, ILogger logger, ResourceNotificationService notificationService, CancellationToken cancellationToken)
    {
        logger.LogInformation("{TimeStamp}: Pulling ollama model {Model}...",
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            model);
        await notificationService.PublishUpdateAsync(resource, state => state with { State = new ResourceStateSnapshot($"Downloading {model}", KnownResourceStateStyles.Info) });

        long percentage = 0;

        try
        {
            await foreach (PullModelResponse? status in ollamaClient.PullModelAsync(model, cancellationToken))
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
                        await notificationService.PublishUpdateAsync(resource,
                        state => state with
                        {
                            State = new ResourceStateSnapshot(percentageState, KnownResourceStateStyles.Info)
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error pulling model");
            throw;
        }

        logger.LogInformation("{TimeStamp}: Finished pulling ollama model {Model}",
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            model);
    }

}
