using CommunityToolkit.Aspire.VLLM;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenTelemetry;
using System.ClientModel;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for registering an <see cref="IChatClient"/> backed by a vLLM server.
/// </summary>
public static class AspireVLLMChatClientExtensions
{
    private const string MeaiTelemetrySourceName = "Experimental.Microsoft.Extensions.AI";

    /// <summary>
    /// Registers a singleton <see cref="IChatClient"/> that targets the vLLM OpenAI-compatible API.
    /// </summary>
    /// <param name="builder">An <see cref="AspireVLLMClientBuilder" />.</param>
    /// <param name="modelId">The served model name to use. Defaults to <see cref="VLLMClientSettings.Model"/>.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the chat client is resolved and no model name was provided.</exception>
    public static ChatClientBuilder AddChatClient(this AspireVLLMClientBuilder builder, string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddTelemetrySource(builder);

        return builder.HostBuilder.Services.AddChatClient(
            services => CreateInnerChatClient(services, builder, modelId));
    }

    /// <summary>
    /// Registers a keyed singleton <see cref="IChatClient"/> that targets the vLLM OpenAI-compatible API,
    /// using the service key of the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireVLLMClientBuilder" /> created by <c>AddKeyedVLLMClient</c>.</param>
    /// <param name="modelId">The served model name to use. Defaults to <see cref="VLLMClientSettings.Model"/>.</param>
    /// <returns>A <see cref="ChatClientBuilder"/> that can be used to build a pipeline around the inner <see cref="IChatClient"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the builder was not created with a service key.</exception>
    public static ChatClientBuilder AddKeyedChatClient(this AspireVLLMClientBuilder builder, string? modelId = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.ServiceKey is null)
        {
            throw new InvalidOperationException(
                $"{nameof(AddKeyedChatClient)} requires a keyed vLLM client. Register the client with {nameof(AspireVLLMExtensions.AddKeyedVLLMClient)}.");
        }

        AddTelemetrySource(builder);

        return builder.HostBuilder.Services.AddKeyedChatClient(
            builder.ServiceKey,
            services => CreateInnerChatClient(services, builder, modelId));
    }

    /// <summary>
    /// Creates the OpenAI-compatible chat client and wraps it in a telemetry client if tracing is enabled.
    /// This doesn't use <c>UseOpenTelemetry()</c> so that the telemetry client stays innermost in the pipeline.
    /// </summary>
    private static IChatClient CreateInnerChatClient(IServiceProvider services, AspireVLLMClientBuilder builder, string? modelId)
    {
        var settings = builder.Settings;
        var model = modelId ?? settings.Model
            ?? throw new InvalidOperationException(
                $"A vLLM served model name is required. Provide it with AddChatClient(modelId), {nameof(VLLMClientSettings)}.{nameof(VLLMClientSettings.Model)}, " +
                $"the '{AspireVLLMExtensions.DefaultConfigSectionName}:{builder.ConnectionName}:Model' configuration value, or the 'Model' connection string value.");

        var apiKey = string.IsNullOrEmpty(settings.Key) ? VLLMClientSettings.PlaceholderApiKey : settings.Key;
        var endpoint = new Uri($"{settings.Endpoint!.AbsoluteUri.TrimEnd('/')}/v1");

        IChatClient chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = endpoint })
            .GetChatClient(model)
            .AsIChatClient();

        if (builder.DisableTracing)
        {
            return chatClient;
        }

        var loggerFactory = services.GetService<ILoggerFactory>();
        return new OpenTelemetryChatClient(chatClient, loggerFactory?.CreateLogger(typeof(OpenTelemetryChatClient)), MeaiTelemetrySourceName)
        {
            EnableSensitiveData = settings.EnableSensitiveTelemetryData,
        };
    }

    private static void AddTelemetrySource(AspireVLLMClientBuilder builder)
    {
        if (builder.DisableTracing)
        {
            return;
        }

        builder.HostBuilder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(MeaiTelemetrySourceName));
    }
}
