using Bitwarden.Sdk;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Bitwarden.SecretManager;

internal sealed class BitwardenSecretManagerHealthCheck(
    BitwardenClient client,
    BitwardenSecretManagerClientSettings settings) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = client.Secrets.Sync(settings.OrganizationId, null);
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(exception: ex));
        }
    }
}