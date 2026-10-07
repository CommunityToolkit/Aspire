using ChromaDB.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Chroma;

/// <summary>
/// Checks the health of a ChromaDB server with its heartbeat.
/// </summary>
/// <param name="chromaClient">The client of the server to check.</param>
internal sealed class ChromaHealthCheck(ChromaClient chromaClient) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await chromaClient.HeartbeatAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
