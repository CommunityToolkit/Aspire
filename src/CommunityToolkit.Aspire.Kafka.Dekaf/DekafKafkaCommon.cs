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
    /// <summary>Normalizes comma-separated bootstrap servers and replaces the entire native server list when overridden.</summary>
    /// <param name="configuration">The native client options section.</param>
    /// <param name="bootstrapServers">The optional Aspire connection string override.</param>
    /// <returns>Native options with bootstrap servers represented as an indexed list.</returns>
    internal static IConfiguration NormalizeBootstrapServers(IConfiguration configuration, string? bootstrapServers = null)
    {
        bootstrapServers ??= configuration["BootstrapServers"];
        if (bootstrapServers is null)
        {
            return configuration;
        }

        // Remove indexed entries too: a shorter override must not retain old backup brokers.
        var entries = configuration.AsEnumerable(makePathsRelative: true).Where(entry =>
            !entry.Key.Equals("BootstrapServers", StringComparison.OrdinalIgnoreCase)
            && !entry.Key.StartsWith("BootstrapServers:", StringComparison.OrdinalIgnoreCase));
        var servers = bootstrapServers.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select((server, index) => new KeyValuePair<string, string?>($"BootstrapServers:{index}", server));
        return new ConfigurationBuilder().AddInMemoryCollection(entries.Concat(servers)).Build();
    }

    /// <summary>
    /// Combines default settings with the overrides for a named connection.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="sectionName">The configuration section for the client role.</param>
    /// <param name="connectionName">The named connection whose overrides take precedence.</param>
    /// <param name="connectionPaths">Alternative connection fields that must be replaced together when the named section supplies one.</param>
    /// <returns>The merged client configuration.</returns>
    internal static IConfigurationRoot GetConfiguration(IHostApplicationBuilder builder, string sectionName, string connectionName, params string[] connectionPaths)
    {
        var section = builder.Configuration.GetSection(sectionName);
        var namedSection = section.GetSection(connectionName);
        var replaceConnection = connectionPaths.Any(path => namedSection.GetSection(path).Exists());
        var defaults = section.AsEnumerable(makePathsRelative: true).Where(entry =>
            !replaceConnection || !connectionPaths.Any(path =>
                entry.Key.Equals(path, StringComparison.OrdinalIgnoreCase)
                || entry.Key.StartsWith(path + ":", StringComparison.OrdinalIgnoreCase)));

        // Merge before passing native configuration to Dekaf so default and named options
        // are applied together. Replace connection lists as a unit: merging their indexed
        // keys can leave a default backup broker or registry from a different cluster.
        return new ConfigurationBuilder()
            .AddInMemoryCollection(defaults)
            .AddInMemoryCollection(namedSection.AsEnumerable(makePathsRelative: true))
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

    /// <summary>Distinguishes health checks for different closed generic client registrations.</summary>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="role">The client role.</param>
    /// <param name="serviceKey">The service key, or null for an unkeyed registration.</param>
    /// <returns>The health check registration name.</returns>
    internal static string GetHealthCheckName<TKey, TValue>(string role, string? serviceKey)
        => GetHealthCheckName($"{role}<{typeof(TKey)},{typeof(TValue)}>", serviceKey);

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
