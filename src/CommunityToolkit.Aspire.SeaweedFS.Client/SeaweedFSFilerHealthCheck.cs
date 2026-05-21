using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.SeaweedFS.Client;

/// <summary>
/// Represents a health check for the SeaweedFS Filer API.
/// </summary>
internal sealed class SeaweedFSFilerHealthCheck(SeaweedFSFilerClient filerClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Send a lightweight GET request to the root of the Filer API. 
            // Using Accept: application/json ensures we get a small JSON directory listing rather than HTML payload.
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            request.Headers.Add("Accept", "application/json");

            var response = await filerClient.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy();
            }

            return new HealthCheckResult(context.Registration.FailureStatus, $"SeaweedFS Filer API responded with HTTP {response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "SeaweedFS Filer API health check failed.", ex);
        }
    }
}