using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.SeaweedFS;

/// <summary>
/// A smart readiness probe that ensures the SeaweedFS cluster is not marked as healthy 
/// until the Volume Server has fully mapped the data nodes to the Master Server.
/// </summary>
internal sealed class SeaweedFSHealthCheck(SeaweedFSContainerResource resource) : IHealthCheck
{
    // Use a shared HttpClient to prevent socket exhaustion during periodic health checks.
    // PooledConnectionLifetime prevents DNS caching issues in dynamic container environments.
    private static readonly HttpClient s_httpClient = new HttpClient(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5)
    })
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var masterEndpoint = resource.GetEndpoint(SeaweedFSContainerResource.MasterEndpointName);

            // Evaluates the endpoint. If the container is still booting network interfaces, it will throw safely.
            var dirStatusUrl = $"{masterEndpoint.Url}/dir/status";

            var response = await s_httpClient.GetAsync(dirStatusUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Unhealthy($"Master API is unreachable. HTTP {response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Validates the Eventual Consistency of the cluster topology
            if (root.TryGetProperty("Topology", out var topology) && topology.TryGetProperty("Max", out var max))
            {
                if (max.GetInt64() == 0)
                {
                    return HealthCheckResult.Unhealthy("Master is online, but waiting for Volume Server to map data nodes...");
                }
            }
            else
            {
                return HealthCheckResult.Unhealthy("Invalid topology response from Master.");
            }

            return HealthCheckResult.Healthy("SeaweedFS Cluster is fully operational and data volumes are registered.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to connect to SeaweedFS Master API.", ex);
        }
    }
}