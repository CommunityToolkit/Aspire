using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methos for configuring the <see cref="IChatClient" /> from an <see cref="OllamaApiClient"/> 
/// </summary>
public static class AspireOllamaChatClientExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="IChatClient"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOllamaApiClientBuilder" />.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    public static ChatClientBuilder AddChatClient(this AspireOllamaApiClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.HostBuilder.Services.AddChatClient(
                services => CreateInnerChatClient(services, builder));
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
        ArgumentException.ThrowIfNullOrEmpty(builder.ServiceKey, nameof(builder.ServiceKey));

        return builder.HostBuilder.Services.AddKeyedChatClient(
                builder.ServiceKey,
                services => CreateInnerChatClient(services, builder));
    }

    /// <summary>
    /// Wrap the <see cref="IOllamaApiClient"/> in a telemetry client if tracing is enabled.
    /// Note that this doesn't use ".UseOpenTelemetry()" because the order of the clients would be incorrect.
    /// We want the telemetry client to be the innermost client, right next to the inner <see cref="IOllamaApiClient"/>.
    /// </summary>
    private static IChatClient CreateInnerChatClient(
        IServiceProvider services,
        AspireOllamaApiClientBuilder builder)
    {
        var ollamaApiClient = services.GetRequiredKeyedService<IOllamaApiClient>(builder.ServiceKey);

        var result = (IChatClient)ollamaApiClient;

        if (builder.DisableTracing)
        {
            return result;
        }

        var loggerFactory = services.GetService<ILoggerFactory>();
        return new OpenTelemetryChatClient(result, loggerFactory?.CreateLogger(typeof(OpenTelemetryChatClient)));
    }
}
