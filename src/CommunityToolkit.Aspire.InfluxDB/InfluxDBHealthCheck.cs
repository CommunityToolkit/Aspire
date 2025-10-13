// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using InfluxDB.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.InfluxDB;

internal sealed class InfluxDBHealthCheck : IHealthCheck
{
    private readonly InfluxDBClient _influxDBClient;

    public InfluxDBHealthCheck(InfluxDBClient influxDBClient)
    {
        ArgumentNullException.ThrowIfNull(influxDBClient, nameof(influxDBClient));
        _influxDBClient = influxDBClient;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _influxDBClient.HealthAsync(cancellationToken).ConfigureAwait(false);

            return health.Status == InfluxDB.Client.Api.Domain.HealthCheck.StatusEnum.Pass
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, description: health.Message);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception: ex);
        }
    }
}
