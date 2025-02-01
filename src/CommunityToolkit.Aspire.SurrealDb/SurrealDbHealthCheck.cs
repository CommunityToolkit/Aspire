// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using SurrealDb.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.SurrealDb;

internal sealed class SurrealDbHealthCheck : IHealthCheck
{
    private readonly ISurrealDbClient _surrealdbClient;

    public SurrealDbHealthCheck(ISurrealDbClient surrealdbClient)
    {
        ArgumentNullException.ThrowIfNull(surrealdbClient, nameof(surrealdbClient));
        _surrealdbClient = surrealdbClient;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            bool isHealthy = await _surrealdbClient.Health(cancellationToken).ConfigureAwait(false);

            return isHealthy
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
