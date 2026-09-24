// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dekaf.Extensions.HealthChecks;

namespace CommunityToolkit.Aspire.Kafka.Dekaf;

/// <summary>Provides settings for a Dekaf root client with shared connections and memory budgeting.</summary>
public sealed class KafkaClientSettings
{
    /// <summary>Gets or sets the comma-separated bootstrap servers.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Gets or sets whether broker connectivity health checks are disabled. The default is false.</summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>Gets or sets the broker connectivity health check options.</summary>
    public DekafBrokerHealthCheckOptions HealthCheck { get; set; } = new();

    /// <summary>Gets or sets whether this registration adds the shared Dekaf meter to OpenTelemetry. The default is false.</summary>
    public bool DisableMetrics { get; set; }

    /// <summary>Gets or sets whether this registration adds the shared Dekaf activity source to OpenTelemetry. The default is false.</summary>
    public bool DisableTracing { get; set; }
}
