// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dekaf.Consumer.DeadLetter;

namespace CommunityToolkit.Aspire.Kafka.Dekaf;

/// <summary>
/// Provides settings for a Dekaf Kafka share consumer (KIP-932).
/// </summary>
public sealed class KafkaShareConsumerSettings
{
    /// <summary>Gets or sets the comma-separated bootstrap servers.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Gets or sets whether health checks are disabled. The default is false.</summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>Gets or sets whether this registration adds the shared Dekaf meter to OpenTelemetry. The default is false.</summary>
    public bool DisableMetrics { get; set; }

    /// <summary>Gets or sets whether this registration adds the shared Dekaf activity source to OpenTelemetry. The default is false.</summary>
    public bool DisableTracing { get; set; }

    /// <summary>Gets or sets optional native dead-letter queue configuration for use by Dekaf hosted share consumer services.</summary>
    public Action<DeadLetterQueueBuilder>? ConfigureDeadLetterQueue { get; set; }
}
