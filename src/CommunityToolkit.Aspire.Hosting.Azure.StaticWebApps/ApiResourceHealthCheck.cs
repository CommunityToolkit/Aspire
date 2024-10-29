using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps;

internal sealed class ApiResourceHealthCheck(SwaResource resource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!resource.TryGetAnnotationsOfType<SwaApiEndpointAnnotation>(out var appResource))
        {
            return HealthCheckResult.Healthy();
        }

        try
        {
            var endpoint = appResource.First().Endpoint;
            using var client = new HttpClient();

            var response = await client.GetAsync(endpoint, cancellationToken);
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
