// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Aspire;
using CommunityToolkit.Aspire.Kafka.Dekaf;
using Dekaf;
using Dekaf.Consumer.DeadLetter;
using Dekaf.Extensions.DependencyInjection;
using Dekaf.ShareConsumer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for registering Kafka share consumers (KIP-932).
/// </summary>
public static partial class AspireDekafKafkaShareConsumerExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Kafka:Dekaf:ShareConsumer";

    /// <inheritdoc cref="AddDekafKafkaShareConsumer{TKey,TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider,ShareConsumerBuilder{TKey,TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string connectionName)
        => AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, null, null, connectionName, serviceKey: null);

    /// <inheritdoc cref="AddDekafKafkaShareConsumer{TKey,TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider,ShareConsumerBuilder{TKey,TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string connectionName, Action<KafkaShareConsumerSettings>? configureSettings)
        => AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, configureSettings, null, connectionName, serviceKey: null);

    /// <inheritdoc cref="AddDekafKafkaShareConsumer{TKey,TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider,ShareConsumerBuilder{TKey,TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string connectionName, Action<ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
        => AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, null, Wrap(configureBuilder), connectionName, serviceKey: null);

    /// <inheritdoc cref="AddDekafKafkaShareConsumer{TKey,TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider,ShareConsumerBuilder{TKey,TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string connectionName, Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
        => AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, null, configureBuilder, connectionName, serviceKey: null);

    /// <inheritdoc cref="AddDekafKafkaShareConsumer{TKey,TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider,ShareConsumerBuilder{TKey,TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string connectionName, Action<KafkaShareConsumerSettings>? configureSettings, Action<ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
        => AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, configureSettings, Wrap(configureBuilder), connectionName, serviceKey: null);

    /// <summary>
    /// Registers <see cref="IKafkaShareConsumer{TKey,TValue}"/> as a singleton in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional method for customizing the <see cref="KafkaShareConsumerSettings"/>.</param>
    /// <param name="configureBuilder">An optional method used for customizing the <see cref="ShareConsumerBuilder{TKey,TValue}"/>.</param>
    /// <remarks>
    /// Reads configuration from <c>Aspire:Kafka:Dekaf:ShareConsumer</c> and its named subsection.
    /// Native <c>Config</c> options are applied before the builder callback. The host initializes the client at startup.
    /// <code>builder.AddDekafKafkaShareConsumer&lt;string, string&gt;("messaging");</code>
    /// </remarks>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string connectionName, Action<KafkaShareConsumerSettings>? configureSettings, Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
        => AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, configureSettings, configureBuilder, connectionName, serviceKey: null);

    /// <inheritdoc cref="AddKeyedDekafKafkaShareConsumer{TKey, TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider, ShareConsumerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddKeyedDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, null, null, connectionName: name, serviceKey: name);
    }

    /// <inheritdoc cref="AddKeyedDekafKafkaShareConsumer{TKey, TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider, ShareConsumerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddKeyedDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string name, Action<KafkaShareConsumerSettings>? configureSettings)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, configureSettings, null, connectionName: name, serviceKey: name);
    }

    /// <inheritdoc cref="AddKeyedDekafKafkaShareConsumer{TKey, TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider, ShareConsumerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddKeyedDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string name, Action<ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, null, Wrap(configureBuilder), connectionName: name, serviceKey: name);
    }

    /// <inheritdoc cref="AddKeyedDekafKafkaShareConsumer{TKey, TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider, ShareConsumerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddKeyedDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string name, Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, null, configureBuilder, connectionName: name, serviceKey: name);
    }

    /// <inheritdoc cref="AddKeyedDekafKafkaShareConsumer{TKey, TValue}(IHostApplicationBuilder, string, Action{KafkaShareConsumerSettings}?, Action{IServiceProvider, ShareConsumerBuilder{TKey, TValue}}?)"/>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddKeyedDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string name, Action<KafkaShareConsumerSettings>? configureSettings, Action<ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, configureSettings, Wrap(configureBuilder), connectionName: name, serviceKey: name);
    }

    /// <summary>
    /// Registers <see cref="IKafkaShareConsumer{TKey,TValue}"/> as a keyed singleton for the given <paramref name="name"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="name">The name of the component, which is used as the <see cref="ServiceDescriptor.ServiceKey"/> of the service and also to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional method for customizing the <see cref="KafkaShareConsumerSettings"/>.</param>
    /// <param name="configureBuilder">An optional method used for customizing the <see cref="ShareConsumerBuilder{TKey,TValue}"/>.</param>
    /// <remarks>
    /// Reads configuration from <c>Aspire:Kafka:Dekaf:ShareConsumer</c> and its named subsection.
    /// <code>builder.AddKeyedDekafKafkaShareConsumer&lt;string, string&gt;("messaging");</code>
    /// </remarks>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddKeyedDekafKafkaShareConsumer<TKey, TValue>(this IHostApplicationBuilder builder, string name, Action<KafkaShareConsumerSettings>? configureSettings, Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        AddDekafKafkaShareConsumerInternal<TKey, TValue>(builder, configureSettings, configureBuilder, connectionName: name, serviceKey: name);
    }

    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    private static void AddDekafKafkaShareConsumerInternal<TKey, TValue>(
        IHostApplicationBuilder builder,
        Action<KafkaShareConsumerSettings>? configureSettings,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder,
        string connectionName,
        string? serviceKey,
        Action<DekafBuilder, Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>, Action<DeadLetterQueueBuilder>?>? registerConsumer = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        var configuration = DekafKafkaCommon.GetConfiguration(builder, DefaultConfigSectionName, connectionName, "Config:BootstrapServers");
        var settings = configuration.Get<KafkaShareConsumerSettings>() ?? new();
        settings.ConnectionString = builder.Configuration.GetConnectionString(connectionName) ?? settings.ConnectionString;
        configureSettings?.Invoke(settings);

        builder.Services.AddDekaf(dekaf =>
        {
            void Configure(IServiceProvider services, ShareConsumerBuilder<TKey, TValue> client)
            {
                KafkaShareConsumerConfiguration.Apply(configuration.GetSection("Config"), client);
                if (settings.ConnectionString is not null)
                {
                    client.WithBootstrapServers(settings.ConnectionString);
                }

                configureBuilder?.Invoke(services, client);
            }

            if (registerConsumer is not null)
            {
                registerConsumer(dekaf, Configure, settings.ConfigureDeadLetterQueue);
            }
            else if (serviceKey is null)
            {
                dekaf.AddShareConsumer<TKey, TValue>(Configure, settings.ConfigureDeadLetterQueue);
            }
            else
            {
                dekaf.AddShareConsumer<TKey, TValue>(serviceKey, Configure, settings.ConfigureDeadLetterQueue);
            }
        });

        DekafKafkaCommon.AddTelemetry(builder, settings.DisableMetrics, settings.DisableTracing);

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = DekafKafkaCommon.GetHealthCheckName<TKey, TValue>("shareconsumer", serviceKey);
            builder.TryAddHealthCheck(new HealthCheckRegistration(healthCheckName,
                services => new KafkaShareConsumerHealthCheck<TKey, TValue>(
                    serviceKey is null
                        ? services.GetRequiredService<IKafkaShareConsumer<TKey, TValue>>()
                        : services.GetRequiredKeyedService<IKafkaShareConsumer<TKey, TValue>>(serviceKey)),
                failureStatus: default, tags: default));
        }
    }

    private static Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? Wrap<TKey, TValue>(Action<ShareConsumerBuilder<TKey, TValue>>? action)
        => action is null ? null : (_, builder) => action(builder);
}
