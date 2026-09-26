// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Aspire;
using CommunityToolkit.Aspire.Kafka.Dekaf;
using Dekaf;
using Dekaf.Admin;
using Dekaf.Extensions.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

/// <summary>Registers Dekaf root clients for sharing connections and memory budgets between related clients.</summary>
public static class AspireDekafKafkaClientExtensions
{
    /// <summary>Registers a singleton <see cref="KafkaClient"/> with broker health checks and OpenTelemetry.</summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="connectionName">The connection string name.</param>
    /// <param name="configureSettings">Optional Aspire settings customization.</param>
    /// <param name="configureBuilder">Optional root builder customization using application services.</param>
    /// <remarks>Reads settings from <c>Aspire:Kafka:Dekaf:Client</c> and its named subsection. The host owns the root client. Applications must initialize and dispose the child clients they create.</remarks>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddDekafKafkaClient(this IHostApplicationBuilder builder, string connectionName,
        Action<KafkaClientSettings>? configureSettings = null,
        Action<IServiceProvider, KafkaClientBuilder>? configureBuilder = null)
        => AddClient(builder, connectionName, serviceKey: null, configureSettings, configureBuilder);

    /// <summary>Registers a keyed singleton <see cref="KafkaClient"/> with broker health checks and OpenTelemetry.</summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional Aspire settings customization.</param>
    /// <param name="configureBuilder">Optional root builder customization using application services.</param>
    /// <remarks>The host owns the root client. Applications must initialize and dispose the child clients they create.</remarks>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddKeyedDekafKafkaClient(this IHostApplicationBuilder builder, string name,
        Action<KafkaClientSettings>? configureSettings = null,
        Action<IServiceProvider, KafkaClientBuilder>? configureBuilder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddClient(builder, name, name, configureSettings, configureBuilder);
    }

    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    private static void AddClient(IHostApplicationBuilder builder, string connectionName, string? serviceKey,
        Action<KafkaClientSettings>? configureSettings, Action<IServiceProvider, KafkaClientBuilder>? configureBuilder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);
        var configuration = DekafKafkaCommon.GetConfiguration(builder, "Aspire:Kafka:Dekaf:Client", connectionName);
        var settings = configuration.Get<KafkaClientSettings>() ?? new();
        settings.ConnectionString = builder.Configuration.GetConnectionString(connectionName) ?? settings.ConnectionString;
        configureSettings?.Invoke(settings);

        KafkaClient CreateClient(IServiceProvider services)
        {
            var client = new KafkaClientBuilder().WithLoggerFactory(services.GetRequiredService<ILoggerFactory>());
            if (settings.ConnectionString is not null)
            {
                client.WithBootstrapServers(settings.ConnectionString);
            }
            configureBuilder?.Invoke(services, client);
            return client.Build();
        }

        KafkaClient GetClient(IServiceProvider services) => serviceKey is null
            ? services.GetRequiredService<KafkaClient>()
            : services.GetRequiredKeyedService<KafkaClient>(serviceKey);

        if (serviceKey is null)
        {
            builder.Services.AddSingleton(CreateClient);
        }
        else
        {
            builder.Services.AddKeyedSingleton<KafkaClient>(serviceKey, (services, _) => CreateClient(services));
        }

        DekafKafkaCommon.AddTelemetry(builder, settings.DisableMetrics, settings.DisableTracing);
        if (!settings.DisableHealthChecks)
        {
            // DI owns the health-check client and disposes it before its shared root infrastructure.
            var healthClientKey = new object();
            builder.Services.AddKeyedSingleton<IAdminClient>(healthClientKey, (services, _) => GetClient(services).CreateAdminClient().Build());
            builder.TryAddHealthCheck(new HealthCheckRegistration(DekafKafkaCommon.GetHealthCheckName("client", serviceKey),
                services => new DekafBrokerHealthCheck(services.GetRequiredKeyedService<IAdminClient>(healthClientKey), settings.HealthCheck),
                failureStatus: default, tags: default));
        }
    }
}
