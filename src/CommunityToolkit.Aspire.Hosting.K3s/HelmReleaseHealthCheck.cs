using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Hosting;

/// <summary>
/// Lightweight health check that gates <c>WaitFor(helmRelease)</c> on completion of
/// the helm install lifecycle. Returns <see cref="HealthCheckResult.Healthy"/> only
/// after <see cref="HelmReleaseResource.IsReady"/> is set by <c>RunReleaseAsync</c>.
/// </summary>
internal sealed class HelmReleaseHealthCheck(HelmReleaseResource release) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(release.IsReady
            ? HealthCheckResult.Healthy("Helm release is running")
            : HealthCheckResult.Unhealthy("Helm release not yet ready"));
}
