// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Aspire;
using CommunityToolkit.Aspire.Kafka.Dekaf;
using Dekaf.Admin;
using Dekaf.Extensions.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for registering Dekaf Kafka admin clients.
/// </summary>
public static class AspireDekafKafkaAdminClientExtensions
{
    /// <summary>
    /// Registers <see cref="IAdminClient"/> as a singleton with a broker connectivity health check.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="connectionName">The name of the connection string.</param>
    /// <param name="configureSettings">Optional settings customization.</param>
    /// <param name="clientFactory">Optional client factory receiving application services and the bound native options. The host owns the returned client.</param>
    /// <remarks>
    /// Reads settings from <c>Aspire:Kafka:Dekaf:AdminClient</c> and its named subsection.
    /// The health check describes the cluster without creating topics or publishing messages.
    /// <code>builder.AddDekafKafkaAdminClient("messaging");</code>
    /// </remarks>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddDekafKafkaAdminClient(this IHostApplicationBuilder builder, string connectionName,
        Action<KafkaAdminClientSettings>? configureSettings = null,
        Func<IServiceProvider, AdminClientOptions, IAdminClient>? clientFactory = null)
        => AddAdminClient(builder, connectionName, serviceKey: null, configureSettings, clientFactory);

    /// <summary>
    /// Registers <see cref="IAdminClient"/> as a keyed singleton with a broker connectivity health check.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional settings customization.</param>
    /// <param name="clientFactory">Optional client factory receiving application services and the bound native options. The host owns the returned client.</param>
    /// <remarks>
    /// Reads settings from <c>Aspire:Kafka:Dekaf:AdminClient</c> and its named subsection.
    /// <code>builder.AddKeyedDekafKafkaAdminClient("messaging");</code>
    /// </remarks>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddKeyedDekafKafkaAdminClient(this IHostApplicationBuilder builder, string name,
        Action<KafkaAdminClientSettings>? configureSettings = null,
        Func<IServiceProvider, AdminClientOptions, IAdminClient>? clientFactory = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddAdminClient(builder, name, serviceKey: name, configureSettings, clientFactory);
    }

    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    private static void AddAdminClient(IHostApplicationBuilder builder, string connectionName, string? serviceKey,
        Action<KafkaAdminClientSettings>? configureSettings,
        Func<IServiceProvider, AdminClientOptions, IAdminClient>? clientFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        var configuration = DekafKafkaCommon.GetConfiguration(builder, "Aspire:Kafka:Dekaf:AdminClient", connectionName,
            "Config:BootstrapServers", "Config:BootstrapControllers");
        var settings = configuration.Get<KafkaAdminClientSettings>() ?? new();
        settings.ConnectionString = builder.Configuration.GetConnectionString(connectionName) ?? settings.ConnectionString;
        configureSettings?.Invoke(settings);

        var nativeConfiguration = DekafKafkaCommon.NormalizeBootstrapServers(configuration.GetSection("Config"), settings.ConnectionString);
        if (settings.ConnectionString is not null)
        {
            // An Aspire broker connection replaces controller-only configuration too.
            // Dekaf rejects options containing both bootstrap servers and controllers.
            nativeConfiguration = new ConfigurationBuilder().AddInMemoryCollection(nativeConfiguration.AsEnumerable()
                .Where(entry => !entry.Key.Equals("BootstrapControllers", StringComparison.OrdinalIgnoreCase)
                    && !entry.Key.StartsWith("BootstrapControllers:", StringComparison.OrdinalIgnoreCase))).Build();
        }
        var nativeOptions = new AdminClientOptions { BootstrapServers = [] };
        nativeConfiguration.Bind(nativeOptions);

        IAdminClient CreateClient(IServiceProvider services)
        {
            if (clientFactory is not null)
            {
                return clientFactory(services, nativeOptions);
            }
            if (nativeOptions.BootstrapServers.Count == 0 && nativeOptions.BootstrapControllers.Count == 0)
            {
                throw new InvalidOperationException("Bootstrap servers or controllers must be specified.");
            }
            return new AdminClient(nativeOptions, services.GetRequiredService<ILoggerFactory>());
        }

        if (serviceKey is null)
        {
            builder.Services.AddSingleton(CreateClient);
        }
        else
        {
            builder.Services.AddKeyedSingleton<IAdminClient>(serviceKey, (services, _) => CreateClient(services));
        }

        DekafKafkaCommon.AddTelemetry(builder, settings.DisableMetrics, settings.DisableTracing);

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = DekafKafkaCommon.GetHealthCheckName("admin", serviceKey);
            builder.TryAddHealthCheck(new HealthCheckRegistration(healthCheckName,
                services => new DekafBrokerHealthCheck(
                    serviceKey is null
                        ? services.GetRequiredService<IAdminClient>()
                        : services.GetRequiredKeyedService<IAdminClient>(serviceKey),
                    settings.HealthCheck),
                failureStatus: default, tags: default));
        }
    }
}
