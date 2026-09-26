// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Aspire.Kafka.Dekaf;
using Dekaf;
using Dekaf.Extensions.Hosting;

namespace Microsoft.Extensions.Hosting;

public static partial class AspireDekafKafkaShareConsumerExtensions
{
    /// <summary>Registers a share consumer and starts a Dekaf hosted service that processes its records.</summary>
    /// <typeparam name="TService">The hosted share consumer service type.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <param name="connectionName">The connection string name.</param>
    /// <param name="configureSettings">Optional settings and dead-letter queue configuration.</param>
    /// <param name="configureBuilder">Optional native builder customization using application services.</param>
    /// <remarks>The service uses explicit acknowledgements, accepts successfully processed records, and owns shutdown and dead-letter routing.</remarks>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddDekafKafkaShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this IHostApplicationBuilder builder, string connectionName,
        Action<KafkaShareConsumerSettings>? configureSettings = null,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
        => AddDekafKafkaShareConsumerInternal(builder, configureSettings, configureBuilder, connectionName, serviceKey: null,
            (dekaf, configure, deadLetter) => dekaf.AddShareConsumerService<TService, TKey, TValue>(configure, deadLetter),
            hostedServiceType: typeof(TService));

    /// <summary>Registers a keyed share consumer and starts an independently configured Dekaf hosted service.</summary>
    /// <typeparam name="TService">The hosted share consumer service type.</typeparam>
    /// <typeparam name="TKey">The message key type.</typeparam>
    /// <typeparam name="TValue">The message value type.</typeparam>
    /// <param name="builder">The host builder.</param>
    /// <param name="name">The service key and connection string name.</param>
    /// <param name="configureSettings">Optional settings and dead-letter queue configuration.</param>
    /// <param name="configureBuilder">Optional native builder customization using application services.</param>
    /// <remarks>The service uses explicit acknowledgements. Each service type and key pair has an independent consumer and dead-letter configuration.</remarks>
    [RequiresDynamicCode("Dekaf configuration binding requires dynamic code.")]
    [RequiresUnreferencedCode("Dekaf configuration binding requires unreferenced members to be preserved.")]
    public static void AddKeyedDekafKafkaShareConsumerService<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService, TKey, TValue>(
        this IHostApplicationBuilder builder, string name,
        Action<KafkaShareConsumerSettings>? configureSettings = null,
        Action<IServiceProvider, ShareConsumerBuilder<TKey, TValue>>? configureBuilder = null)
        where TService : KafkaShareConsumerService<TKey, TValue>
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        AddDekafKafkaShareConsumerInternal(builder, configureSettings, configureBuilder, name, serviceKey: name,
            (dekaf, configure, deadLetter) => dekaf.AddShareConsumerService<TService, TKey, TValue>(name, configure, deadLetter),
            hostedServiceType: typeof(TService));
    }
}
