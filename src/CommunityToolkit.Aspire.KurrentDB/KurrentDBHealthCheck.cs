using KurrentDB.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.KurrentDB;

/// <summary>
/// Checks whether a connection can be made to KurrentDB services using the supplied connection string.
/// </summary>
public class KurrentDBHealthCheck : IHealthCheck, IDisposable
{
    private readonly KurrentDBClient _client;

    /// <inheritdoc/>
    public KurrentDBHealthCheck(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        _client = new KurrentDBClient(KurrentDBClientSettings.Create(connectionString));
    }

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var readAllStreamResult = _client.ReadAllAsync(
                direction: Direction.Backwards,
                position: Position.End,
                maxCount: 1,
                cancellationToken: cancellationToken);

            var events = await readAllStreamResult.ToListAsync(cancellationToken);
            
            if (events.Count > 0)
            {
                return HealthCheckResult.Healthy();
            }

            return new HealthCheckResult(context.Registration.FailureStatus, "Failed to connect to KurrentDB.");
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: exception);
        }
    }

    /// <inheritdoc/>
    public virtual void Dispose() => _client.Dispose();
}