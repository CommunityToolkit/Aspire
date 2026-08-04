// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using SurrealDb.Net;

namespace Aspire.Hosting;

/// <summary>
/// Performs a health check for a SurrealDB server resource by validating that the server is accepting client traffic.
/// </summary>
internal sealed class SurrealDbServerHealthCheck(SurrealDbServerResource server, ILogger<SurrealDbServerHealthCheck> logger) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = await server.ConnectionStringExpression.GetValueAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Connection string for resource '{server.Name}' is not available.");

            var options = new SurrealDbOptionsBuilder().FromConnectionString(connectionString).Build();
            await using var client = new SurrealDbClient(options);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            return await client.Health(cts.Token).ConfigureAwait(false)
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SurrealDB server health check for '{ResourceName}' raised an exception.", server.Name);
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
