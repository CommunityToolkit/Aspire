using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OpenTelemetry;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methos for configuring the <see cref="IChatClient" /> from an <see cref="OllamaApiClient"/> 
/// </summary>
public static class AspireOllamaChatClientExtensions
{
    private const string MeaiTelemetrySourceName = "Experimental.Microsoft.Extensions.AI";

    /// <summary>
    /// Registers a singleton <see cref="IChatClient"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOllamaApiClientBuilder" />.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    public static ChatClientBuilder AddChatClient(this AspireOllamaApiClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.AddChatClient(configureOpenTelemetry: null);
    }

    /// <summary>
    /// Registers a singleton <see cref="IChatClient"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOllamaApiClientBuilder" />.</param>
    /// <param name="configureOpenTelemetry">An optional delegate that can be used for customizing the OpenTelemetry chat client.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    public static ChatClientBuilder AddChatClient(
        this AspireOllamaApiClientBuilder builder,
        Action<OpenTelemetryChatClient>? configureOpenTelemetry)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        AddTelemetrySource(builder.HostBuilder);

        return builder.HostBuilder.Services.AddChatClient(
                services => CreateInnerChatClient(services, builder, configureOpenTelemetry));
    }

    /// <summary>
    /// Registers a keyed singleton <see cref="IChatClient"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOllamaApiClientBuilder" />.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    public static ChatClientBuilder AddKeyedChatClient(
        this AspireOllamaApiClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.AddKeyedChatClient(builder.ServiceKey, configureOpenTelemetry: null);
    }

    /// <summary>
    /// Registers a keyed singleton <see cref="IChatClient"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOllamaApiClientBuilder" />.</param>
    /// <param name="configureOpenTelemetry">An optional delegate that can be used for customizing the OpenTelemetry chat client.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    public static ChatClientBuilder AddKeyedChatClient(
        this AspireOllamaApiClientBuilder builder,
        Action<OpenTelemetryChatClient>? configureOpenTelemetry)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.AddKeyedChatClient(builder.ServiceKey, configureOpenTelemetry);
    }

    /// <summary>
    /// Registers a keyed singleton <see cref="IChatClient"/> in the services provided by the <paramref name="builder"/> using the specified service key.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOllamaApiClientBuilder" />.</param>
    /// <param name="serviceKey">The service key to use for registering the <see cref="IChatClient"/>.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    public static ChatClientBuilder AddKeyedChatClient(
        this AspireOllamaApiClientBuilder builder,
        object serviceKey)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(serviceKey, nameof(serviceKey));

        return builder.AddKeyedChatClient(serviceKey, configureOpenTelemetry: null);
    }

    /// <summary>
    /// Registers a keyed singleton <see cref="IChatClient"/> in the services provided by the <paramref name="builder"/> using the specified service key.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOllamaApiClientBuilder" />.</param>
    /// <param name="serviceKey">The service key to use for registering the <see cref="IChatClient"/>.</param>
    /// <param name="configureOpenTelemetry">An optional delegate that can be used for customizing the OpenTelemetry chat client.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    public static ChatClientBuilder AddKeyedChatClient(
        this AspireOllamaApiClientBuilder builder,
        object serviceKey,
        Action<OpenTelemetryChatClient>? configureOpenTelemetry)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(serviceKey, nameof(serviceKey));

        AddTelemetrySource(builder.HostBuilder);

        return builder.HostBuilder.Services.AddKeyedChatClient(
                serviceKey,
                services => CreateInnerChatClient(services, builder, configureOpenTelemetry));
    }

    /// <summary>
    /// Wrap the <see cref="IOllamaApiClient"/> in a telemetry client if tracing is enabled.
    /// Note that this doesn't use ".UseOpenTelemetry()" because the order of the clients would be incorrect.
    /// We want the telemetry client to be the innermost client, right next to the inner <see cref="IOllamaApiClient"/>.
    /// </summary>
    private static IChatClient CreateInnerChatClient(
        IServiceProvider services,
        AspireOllamaApiClientBuilder builder,
        Action<OpenTelemetryChatClient>? configureOpenTelemetry)
    {
        var ollamaApiClient = services.GetRequiredKeyedService<IOllamaApiClient>(builder.ServiceKey);

        var result = (IChatClient)ollamaApiClient;

        if (builder.DisableTracing)
        {
            return result;
        }

        var loggerFactory = services.GetService<ILoggerFactory>();
        var otelChatClient = new OpenTelemetryChatClient(result, loggerFactory?.CreateLogger(typeof(OpenTelemetryChatClient)), MeaiTelemetrySourceName);
        
        configureOpenTelemetry?.Invoke(otelChatClient);
        
        return otelChatClient;
    }

    /// <summary>
    /// Add the MEAI telemetry source to OpenTelemetry tracing.
    /// </summary>
    private static void AddTelemetrySource(IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.AddSource(MeaiTelemetrySourceName);
            });
    }
}
