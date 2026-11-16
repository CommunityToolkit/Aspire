using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.VLLM;

/// <summary>
/// A health check that probes the vLLM <c>/health</c> endpoint, which succeeds once the model is loaded.
/// </summary>
internal sealed class VLLMHealthCheck(HttpClient httpClient, Uri endpoint) : IHealthCheck
{
    private readonly Uri _healthUri = new(new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/"), "health");

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(_healthUri, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, $"vLLM '/health' returned status code {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "The vLLM '/health' request failed.", ex);
        }
    }
}
