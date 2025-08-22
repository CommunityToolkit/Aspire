// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using SurrealDb.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.SurrealDb;

internal sealed class SurrealDbHealthCheck : IHealthCheck
{
    private readonly SurrealDbOptions _options;
    private readonly ILogger<SurrealDbHealthCheck> _logger;

    public SurrealDbHealthCheck(SurrealDbOptions options, ILogger<SurrealDbHealthCheck> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        bool isHealthy = false;
        
        try
        {
            await using var surrealdbClient = new SurrealDbClient(_options);
            
            isHealthy = await surrealdbClient.Health(cancellationToken).ConfigureAwait(false);
            var response = await surrealdbClient.RawQuery("RETURN 1", cancellationToken: cancellationToken).ConfigureAwait(false);
            response.EnsureAllOks();

            _logger.LogInformation("SurrealDB health check passed. Response: {Response}", response);
            _logger.LogInformation("SurrealDB health check outcome: {Outcome}", isHealthy ? "Healthy" : "Unhealthy");

            return isHealthy
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SurrealDB health check raised an exception. Health check had previously reported: {Outcome}. CancellationToken status: {CancellationTokenStatus}", isHealthy ? "Healthy" : "Unhealthy", cancellationToken.IsCancellationRequested ? "Canceled" : "Active");
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
