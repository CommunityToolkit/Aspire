// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Dekaf;
using Dekaf.Security.Sasl;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;

namespace CommunityToolkit.Aspire.Kafka.Dekaf;

/// <summary>Applies native share consumer options before the service-provider-aware builder callback.</summary>
internal static class KafkaShareConsumerConfiguration
{
    /// <summary>Applies bound options using the public Dekaf builder API.</summary>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="configuration">The native options section.</param>
    /// <param name="builder">The native share consumer builder.</param>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    internal static void Apply<TKey, TValue>(IConfiguration configuration, ShareConsumerBuilder<TKey, TValue> builder)
    {
        // Dekaf 1.21.1 has no overload combining configuration with an IServiceProvider callback.
        // Keep native option binding here so credentials and all transport options survive an
        // Aspire connection-string override, while serializers can still be resolved from DI.
        var options = DekafKafkaCommon.NormalizeBootstrapServers(configuration).Get<ShareConsumerOptions>();
        if (options is null)
        {
            return;
        }

        if (options.BootstrapServers is not null)
        {
            builder.WithBootstrapServers(options.BootstrapServers.ToArray());
        }
        if (options.GroupId is not null)
        {
            builder.WithGroupId(options.GroupId);
        }
        if (options.ClientId is not null)
        {
            builder.WithClientId(options.ClientId);
        }
        if (options.RackId is not null)
        {
            builder.WithRackId(options.RackId);
        }
        builder.WithFetchMinBytes(options.FetchMinBytes);
        builder.WithFetchMaxBytes(options.FetchMaxBytes);
        builder.WithMaxPartitionFetchBytes(options.MaxPartitionFetchBytes);
        builder.WithFetchMaxWaitMs(options.FetchMaxWaitMs);
        builder.WithMaxPollRecords(options.MaxPollRecords);
        if (configuration[nameof(options.AcknowledgementMode)] is not null)
        {
            builder.WithAcknowledgementMode(options.AcknowledgementMode);
        }
        builder.WithShareAcquireMode(options.ShareAcquireMode);
        builder.WithSessionTimeoutMs(options.SessionTimeoutMs);
        builder.WithHeartbeatIntervalMs(options.HeartbeatIntervalMs);
        builder.WithRequestTimeoutMs(options.RequestTimeoutMs);
        builder.WithSocketSendBufferBytes(options.SocketSendBufferBytes);
        builder.WithSocketReceiveBufferBytes(options.SocketReceiveBufferBytes);
        builder.WithConnectionsPerBroker(options.ConnectionsPerBroker);
        builder.WithClientDnsLookup(options.ClientDnsLookup);
        builder.WithSaslScramMaxIterations(options.SaslScramMaxIterations);
        builder.WithRetryBackoff(TimeSpan.FromMilliseconds(options.RetryBackoffMs));
        builder.WithRetryBackoffMax(TimeSpan.FromMilliseconds(options.RetryBackoffMaxMs));
        builder.WithReconnectBackoff(TimeSpan.FromMilliseconds(options.ReconnectBackoffMs));
        builder.WithReconnectBackoffMax(TimeSpan.FromMilliseconds(options.ReconnectBackoffMaxMs));
        builder.WithConnectionsMaxIdle(options.ConnectionsMaxIdleMs < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(options.ConnectionsMaxIdleMs));
        builder.WithConnectionTimeout(options.ConnectionTimeout);
        builder.WithConnectionTimeoutMax(options.ConnectionTimeoutMax);
        builder.WithTcpKeepAlive(options.TcpKeepAliveTime, options.TcpKeepAliveInterval, options.TcpKeepAliveRetryCount);
        builder.WithTcpKeepAlive(options.EnableTcpKeepAlive);
        builder.WithMetadataClusterCheck(options.MetadataClusterCheckEnabled);
        builder.WithBootstrapResolveTimeout(TimeSpan.FromMilliseconds(options.BootstrapResolveTimeoutMs));

        if (options.TlsConfig is not null)
        {
            builder.WithTlsConfig(options.TlsConfig);
        }
        else
        {
            builder.WithTls(options.UseTls);
        }

        switch (options.SaslMechanism)
        {
            case SaslMechanism.None:
                break;
            case SaslMechanism.Plain:
                builder.WithSaslPlain(options.SaslUsername!, options.SaslPassword!);
                break;
            case SaslMechanism.ScramSha256 when options.SaslScramTokenAuth:
                builder.WithSaslScramSha256DelegationToken(options.SaslUsername!, options.SaslPassword!);
                break;
            case SaslMechanism.ScramSha256:
                builder.WithSaslScram256(options.SaslUsername!, options.SaslPassword!);
                break;
            case SaslMechanism.ScramSha512 when options.SaslScramTokenAuth:
                builder.WithSaslScramSha512DelegationToken(options.SaslUsername!, options.SaslPassword!);
                break;
            case SaslMechanism.ScramSha512:
                builder.WithSaslScram512(options.SaslUsername!, options.SaslPassword!);
                break;
            case SaslMechanism.Gssapi:
                builder.WithGssapiConfig(options.GssapiConfig ?? throw new InvalidOperationException("GssapiConfig is required for GSSAPI authentication."));
                break;
            case SaslMechanism.OAuthBearer:
                builder.WithOAuthBearer(options.OAuthBearerConfig ?? throw new InvalidOperationException("OAuthBearerConfig is required for OAuth authentication."));
                break;
            case SaslMechanism.AwsMskIam:
                builder.WithAwsMskIam(options.AwsMskIamConfig);
                break;
            default:
                throw new InvalidOperationException($"Unsupported SASL mechanism: {options.SaslMechanism}.");
        }
    }
}
