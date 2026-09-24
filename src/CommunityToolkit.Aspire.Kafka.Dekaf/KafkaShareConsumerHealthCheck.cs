// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dekaf.Diagnostics;
using Dekaf.Consumer;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CommunityToolkit.Aspire.Kafka.Dekaf;

/// <summary>Checks share group membership without polling or acknowledging records.</summary>
/// <typeparam name="TKey">The message key type.</typeparam>
/// <typeparam name="TValue">The message value type.</typeparam>
/// <param name="consumer">The share consumer to inspect.</param>
internal sealed class KafkaShareConsumerHealthCheck<TKey, TValue>(IKafkaShareConsumer<TKey, TValue> consumer) : IHealthCheck
{
    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (consumer is not IKafkaClientStatusProvider statusProvider)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("The share consumer does not expose operational status."));
        }

        var status = statusProvider.GetStatus();
        var group = status.ConsumerGroup;
        if (status.IsStopped || group is null || !group.HasConsumerGroup || group.State != CoordinatorState.Stable || string.IsNullOrEmpty(group.MemberId)
            || group.TimeSinceLastHeartbeat is not { } elapsed || group.HeartbeatInterval <= TimeSpan.Zero
            || elapsed > group.HeartbeatInterval * 3 || group.LastHeartbeatFailure is not null)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("The share consumer has no live share group membership."));
        }

        // A share group may have more workers than partitions. An idle member is still healthy.
        return Task.FromResult(HealthCheckResult.Healthy("The share consumer has live share group membership."));
    }
}
