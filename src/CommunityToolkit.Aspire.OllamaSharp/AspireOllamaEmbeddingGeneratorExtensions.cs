using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
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
            services => CreateInnerEmbeddingGenerator(services, builder)).UseAspireDefaults();
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
            services => CreateInnerEmbeddingGenerator(services, builder)).UseAspireDefaults();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateInnerEmbeddingGenerator(
        IServiceProvider services,
        AspireOllamaApiClientBuilder builder)
    {
        var ollamaApiClient = services.GetRequiredKeyedService<IOllamaApiClient>(builder.ServiceKey);

        var result = (IEmbeddingGenerator<string, Embedding<float>>)ollamaApiClient;

        return builder.DisableTracing
            ? result
            : new OpenTelemetryEmbeddingGenerator<string, Embedding<float>>(result);
    }

    private static EmbeddingGeneratorBuilder<TKey, TEmbedding> UseAspireDefaults<TKey, TEmbedding>(this EmbeddingGeneratorBuilder<TKey, TEmbedding> builder)
        where TEmbedding : Embedding =>
            builder.UseLogging()
                .UseOpenTelemetry();
}