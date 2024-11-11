using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OllamaSharp;

namespace CommunityToolkit.Aspire.Hosting.Ollama;

internal class OllamaModelHealthCheck(OllamaModelResource modelResource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        (var success, var endpoint) = await OllamaUtilities.TryGetEndpointAsync(modelResource, cancellationToken);

        if (!success || endpoint is null)
        {
            return HealthCheckResult.Unhealthy("Invalid connection string");
        }

        var ollamaClient = new OllamaApiClient(endpoint);

        // this will only return once the model is downloaded
        await ollamaClient.ShowModelAsync(modelResource.ModelName, cancellationToken);

        return HealthCheckResult.Healthy();
    }
}
