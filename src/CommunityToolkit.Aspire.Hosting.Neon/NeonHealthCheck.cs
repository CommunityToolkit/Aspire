// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.Neon;

/// <summary>
/// Health check that reports healthy once a Neon project resource has been fully provisioned
/// (i.e., a connection URI is available).
/// </summary>
internal sealed class NeonHealthCheck(NeonProjectResource resource) : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(resource.ConnectionUri))
        {
            return Task.FromResult(HealthCheckResult.Healthy("Neon project provisioned."));
        }

        return Task.FromResult(
            new HealthCheckResult(
                context?.Registration?.FailureStatus ?? HealthStatus.Unhealthy,
                "Neon project is still being provisioned."));
    }
}

/// <summary>
/// Health check that reports healthy once a Neon database resource has been fully provisioned
/// (i.e., a connection URI is available).
/// </summary>
internal sealed class NeonDatabaseHealthCheck(NeonDatabaseResource resource) : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(resource.ConnectionUri))
        {
            return Task.FromResult(HealthCheckResult.Healthy("Neon database provisioned."));
        }

        return Task.FromResult(
            new HealthCheckResult(
                context?.Registration?.FailureStatus ?? HealthStatus.Unhealthy,
                "Neon database is still being provisioned."));
    }
}
