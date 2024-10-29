using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps;

internal sealed class SwaEmulatorHealthCheck(SwaResource resource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = resource.GetEndpoint("http");

            if (endpoint is null)
            {
                return HealthCheckResult.Healthy();
            }

            using var client = new HttpClient();

            var response = await client.GetAsync(endpoint.Url, cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Received status code {response.StatusCode} from {endpoint}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message);
        }
    }
}
