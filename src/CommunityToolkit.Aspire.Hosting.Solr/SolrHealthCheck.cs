// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.Solr;

/// <summary>
/// Health check for Apache Solr.
/// </summary>
internal sealed class SolrHealthCheck : IHealthCheck
{
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly string _coreName;

    /// <summary>
    /// Initializes a new instance of the <see cref="SolrHealthCheck"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Factory to create HttpClient instances.</param>
    /// <param name="coreName">The name of the Solr core to check.</param>
    public SolrHealthCheck(Func<HttpClient> httpClientFactory, string coreName)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _coreName = coreName ?? throw new ArgumentNullException(nameof(coreName));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory();
            
            // Check the specific core admin ping endpoint
            var coreAdminPingPath = $"/solr/{_coreName}/admin/ping";
            var response = await httpClient.GetAsync(coreAdminPingPath, cancellationToken).ConfigureAwait(false);
            
            return response.StatusCode == HttpStatusCode.OK
                ? HealthCheckResult.Healthy($"Solr core '{_coreName}' is healthy.")
                : HealthCheckResult.Unhealthy($"Solr health check failed with status code {response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Solr core '{_coreName}' health check failed.", ex);
        }
    }
}
