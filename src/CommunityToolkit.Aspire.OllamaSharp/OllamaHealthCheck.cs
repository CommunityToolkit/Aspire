using Microsoft.Extensions.Diagnostics.HealthChecks;
using OllamaSharp;

namespace CommunityToolkit.Aspire.OllamaSharp;

internal sealed class OllamaHealthCheck(IOllamaApiClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await client.IsRunningAsync(cancellationToken).ConfigureAwait(false)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }

}