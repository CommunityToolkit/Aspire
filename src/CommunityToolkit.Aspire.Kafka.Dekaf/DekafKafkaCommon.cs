// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dekaf.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CommunityToolkit.Aspire.Kafka.Dekaf;

/// <summary>
/// Shares configuration, health check naming, and telemetry registration across Kafka clients.
/// </summary>
internal static class DekafKafkaCommon
{
    /// <summary>
    /// Combines default settings with the overrides for a named connection.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="sectionName">The configuration section for the client role.</param>
    /// <param name="connectionName">The named connection whose overrides take precedence.</param>
    /// <returns>The merged client configuration.</returns>
    internal static IConfigurationRoot GetConfiguration(IHostApplicationBuilder builder, string sectionName, string connectionName)
    {
        var section = builder.Configuration.GetSection(sectionName);

        // Merge before passing native configuration to Dekaf so default and named options
        // are applied together. For example, Producer:orders:Config:ClientId
        // overrides Producer:Config:ClientId while retaining other default options.
        return new ConfigurationBuilder()
            .AddInMemoryCollection(section.AsEnumerable(makePathsRelative: true))
            .AddInMemoryCollection(section.GetSection(connectionName).AsEnumerable(makePathsRelative: true))
            .Build();
    }

    /// <summary>
    /// Gets a health check name that distinguishes client roles and keyed registrations.
    /// </summary>
    /// <param name="role">The client role.</param>
    /// <param name="serviceKey">The service key, or null for an unkeyed registration.</param>
    /// <returns>The health check registration name.</returns>
    internal static string GetHealthCheckName(string role, string? serviceKey)
        => serviceKey is null ? $"Kafka.Dekaf_{role}" : $"Kafka.Dekaf_{role}_{serviceKey}";

    /// <summary>
    /// Registers Dekaf's shared OpenTelemetry meter and activity source when enabled.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="disableMetrics">Whether to omit metric registration.</param>
    /// <param name="disableTracing">Whether to omit tracing registration.</param>
    internal static void AddTelemetry(IHostApplicationBuilder builder, bool disableMetrics, bool disableTracing)
    {
        // Dekaf uses a shared source and meter for all clients. These switches control registration
        // with OpenTelemetry; another registration can still enable telemetry for the same source.
        // https://thomhurst.github.io/Dekaf/docs/observability
        if (!disableMetrics)
        {
            builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(DekafDiagnostics.MeterName));
        }

        if (!disableTracing)
        {
            builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(DekafDiagnostics.ActivitySourceName));
        }
    }
}
