// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using SurrealDb.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace CommunityToolkit.Aspire.SurrealDb;

internal sealed class SurrealDbNamespaceHealthCheck(SurrealDbOptions options, string namespaceName, ILogger<SurrealDbNamespaceHealthCheck> logger) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var surrealClient = new SurrealDbClient(options);

            await surrealClient.Use(namespaceName, null!, cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SurrealDB health check raised an exception when checking if the namespace is available. CancellationToken status: {CancellationTokenStatus}", cancellationToken.IsCancellationRequested ? "Canceled" : "Active");
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
