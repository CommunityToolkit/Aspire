// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SurrealDb.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.SurrealDb;

internal sealed class SurrealDbHealthCheck : IHealthCheck
{
    private readonly ISurrealDbClient _surrealdbClient;
    private readonly ILogger<SurrealDbHealthCheck> _logger;

    public SurrealDbHealthCheck(ISurrealDbClient surrealdbClient, ILogger<SurrealDbHealthCheck> logger)
    {
        ArgumentNullException.ThrowIfNull(surrealdbClient, nameof(surrealdbClient));
        _surrealdbClient = surrealdbClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        bool isHealthy = false;
        try
        {
            isHealthy = await _surrealdbClient.Health(cancellationToken).ConfigureAwait(false);
            var response = await _surrealdbClient.RawQuery("RETURN 1", cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureAllOks();

            _logger.LogInformation("SurrealDB health check passed. Response: {Response}", response);
            _logger.LogInformation("SurrealDB health check outcome: {Outcome}", isHealthy ? "Healthy" : "Unhealthy");

            return isHealthy
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SurrealDB health check raised an exception. Health check had previously reported: {Outcome}.", isHealthy ? "Healthy" : "Unhealthy");
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
