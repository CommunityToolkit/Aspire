// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.GoFeatureFlag;

internal sealed class GoFeatureFlagHealthCheck : IHealthCheck
{
    private readonly string _endpoint;

    public GoFeatureFlagHealthCheck(string endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint, nameof(endpoint));
        _endpoint = endpoint;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(_endpoint);
            
            using var response = await httpClient.GetAsync("/health", cancellationToken).ConfigureAwait(false);
            
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
