using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methos for configuring the <see cref="IEmbeddingGenerator{TInput, TEmbedding}" /> from an <see cref="OllamaApiClient"/> 
/// </summary>
public static class AspireOllamaEmbeddingGeneratorExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOllamaApiClientBuilder" />.</param>
    /// <returns>A <see cref="EmbeddingGeneratorBuilder{TInput, TEmbedding}"/> that can be used to build a pipeline around the inner <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.</returns>
    public static EmbeddingGeneratorBuilder<string, Embedding<float>> AddEmbeddingGenerator(
        this AspireOllamaApiClientBuilder builder)
    {
        return builder.HostBuilder.Services.AddEmbeddingGenerator(
            services => CreateInnerEmbeddingGenerator(services, builder));
    }

    /// <summary>
    /// Registers a keyed singleton <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> in the services provided by the <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">An <see cref="AspireOllamaApiClientBuilder" />.</param>
    /// <returns>A <see cref="EmbeddingGeneratorBuilder{TInput, TEmbedding}"/> that can be used to build a pipeline around the inner <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.</returns>
    public static EmbeddingGeneratorBuilder<string, Embedding<float>> AddKeyedEmbeddingGenerator(
        this AspireOllamaApiClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(builder.ServiceKey, nameof(builder.ServiceKey));

        return builder.HostBuilder.Services.AddKeyedEmbeddingGenerator(
            builder.ServiceKey,
            services => CreateInnerEmbeddingGenerator(services, builder));
    }

    /// <summary>
    /// Wrap the <see cref="IOllamaApiClient"/> in a telemetry client if tracing is enabled.
    /// Note that this doesn't use ".UseOpenTelemetry()" because the order of the clients would be incorrect.
    /// We want the telemetry client to be the innermost client, right next to the inner <see cref="IOllamaApiClient"/>.
    /// </summary>
    private static IEmbeddingGenerator<string, Embedding<float>> CreateInnerEmbeddingGenerator(
        IServiceProvider services,
        AspireOllamaApiClientBuilder builder)
    {
        var ollamaApiClient = services.GetRequiredKeyedService<IOllamaApiClient>(builder.ServiceKey);

        var result = (IEmbeddingGenerator<string, Embedding<float>>)ollamaApiClient;

        if (builder.DisableTracing)
        {
            return result;
        }

        var loggerFactory = services.GetService<ILoggerFactory>();
        return new OpenTelemetryEmbeddingGenerator<string, Embedding<float>>(
            result,
            loggerFactory?.CreateLogger(typeof(OpenTelemetryEmbeddingGenerator<string, Embedding<float>>)));
    }
}