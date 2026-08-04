// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting.Kind;

/// <summary>
/// Health check that verifies the Kind control-plane container is running.
/// </summary>
internal sealed class KindHealthCheck : IHealthCheck
{
    private readonly KindClusterManager _manager;

    public KindHealthCheck(KindClusterManager manager)
    {
        _manager = manager;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var isRunning = await _manager.IsControlPlaneRunningAsync(cancellationToken).ConfigureAwait(false);
        return isRunning
            ? HealthCheckResult.Healthy("Kind control-plane is running.")
            : HealthCheckResult.Unhealthy("Kind control-plane is not running.");
    }
}
