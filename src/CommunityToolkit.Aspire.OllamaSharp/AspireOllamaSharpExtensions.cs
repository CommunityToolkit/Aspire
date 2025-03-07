using CommunityToolkit.Aspire.OllamaSharp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OllamaSharp;
using System.Data.Common;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for setting up OllamaSharp client in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireOllamaSharpExtensions
{
    internal const string DefaultConfigSectionName = "Aspire:OllamaSharp";

    /// <summary>
    /// Adds <see cref="IOllamaApiClient"/> services to the container.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="UriFormatException">Thrown when no Ollama endpoint is provided.</exception>
    public static AspireOllamaApiClientBuilder AddOllamaApiClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName, nameof(connectionName));
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        return AddOllamaClientInternal(builder, DefaultConfigSectionName, connectionName, configureSettings: configureSettings);
    }

    /// <summary>
    /// Adds <see cref="IOllamaApiClient"/> services to the container using the <paramref name="connectionName"/> as the service key. 
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="UriFormatException">Thrown when no Ollama endpoint is provided.</exception>
    public static AspireOllamaApiClientBuilder AddKeyedOllamaApiClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName, nameof(connectionName));
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        return AddOllamaClientInternal(builder, $"{DefaultConfigSectionName}:{connectionName}", connectionName, serviceKey: connectionName, configureSettings: configureSettings);
    }

    /// <summary>
    /// Adds <see cref="IOllamaApiClient"/> and <see cref="IChatClient"/> services to the container.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when no Ollama endpoint is provided.</exception>
    [Obsolete("This approach to registering IChatClient is deprecated, use AddOllamaApiClient().AddChatClient() instead.")]
    public static void AddOllamaSharpChatClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        builder.AddKeyedOllamaApiClient(connectionName, configureSettings);
        builder.Services.AddSingleton(sp => ConfigureOllamaChatClient(sp, connectionName));
        builder.Services.AddSingleton(sp => sp.GetRequiredKeyedService<IOllamaApiClient>(connectionName));
    }

    /// <summary>
    /// Adds <see cref="IOllamaApiClient"/> and <see cref="IChatClient"/> services to the container using the <paramref name="connectionName"/> as the service key. 
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when no Ollama endpoint is provided.</exception>
    [Obsolete("This approach to registering IChatClient is deprecated, use AddKeyedOllamaApiClient().AddChatClient() instead.")]
    public static void AddKeyedOllamaSharpChatClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        builder.AddKeyedOllamaApiClient(connectionName, configureSettings);
        builder.Services.AddKeyedSingleton(connectionName, (sp, _) => ConfigureOllamaChatClient(sp, connectionName));
    }

    /// <summary>
    /// Adds <see cref="IOllamaApiClient"/> and <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> services to the container.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when no Ollama endpoint is provided.</exception>
    [Obsolete("This approach to registering IEmbeddingGenerator is deprecated, use AddOllamaApiClient().AddEmbeddingGenerator() instead.")]
    public static void AddOllamaSharpEmbeddingGenerator(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        builder.AddKeyedOllamaSharpChatClient(connectionName, configureSettings);
        builder.Services.AddSingleton(sp => ConfigureOllamaEmbeddingGenerator<string, Embedding<float>>(sp, connectionName));
        builder.Services.AddSingleton(sp => sp.GetRequiredKeyedService<IOllamaApiClient>(connectionName));
    }

    /// <summary>
    /// Adds <see cref="IOllamaApiClient"/> and <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> services to the container using the <paramref name="connectionName"/> as the service key.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when no Ollama endpoint is provided.</exception>
    [Obsolete("This approach to registering IEmbeddingGenerator is deprecated, use AddKeyedOllamaApiClient().AddEmbeddingGenerator() instead.")]
    public static void AddKeyedOllamaSharpEmbeddingGenerator(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        builder.AddKeyedOllamaSharpChatClient(connectionName, configureSettings);
        builder.Services.AddKeyedSingleton(connectionName, (sp, _) => ConfigureOllamaEmbeddingGenerator<string, Embedding<float>>(sp, connectionName));
    }

    private static AspireOllamaApiClientBuilder AddOllamaClientInternal(
        IHostApplicationBuilder builder,
        string configurationSectionName,
        string connectionName,
        string? serviceKey = null,
        Action<OllamaSharpSettings>? configureSettings = null)
    {
        OllamaSharpSettings settings = new();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            var connectionBuilder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (connectionBuilder.ContainsKey("Endpoint") && Uri.TryCreate(connectionBuilder["Endpoint"].ToString(), UriKind.Absolute, out Uri? endpoint))
            {
                settings.Endpoint = endpoint;
            }

            if (connectionBuilder.ContainsKey("Model"))
            {
                settings.SelectedModel = (string)connectionBuilder["Model"];
            }
        }

        configureSettings?.Invoke(settings);

        string httpClientKey = $"{connectionName}_httpClient";

        builder.Services.AddHttpClient(httpClientKey, client =>
        {
            if (settings.Endpoint is not null)
            {
                client.BaseAddress = settings.Endpoint;
            }
            else
            {
                throw new InvalidOperationException(
                    $"An OllamaApiClient could not be configured. Ensure valid connection information was provided in 'ConnectionStrings:{connectionName}' or either " +
                    $"{nameof(settings.Endpoint)} must be provided " +
                    $"in the '{configurationSectionName}' configuration section.");
            }
        });

        if (serviceKey is not null)
        {
            builder.Services.AddKeyedSingleton(serviceKey, (sp, _) => ConfigureOllamaClient(sp));
        }
        else
        {
            builder.Services.AddSingleton(ConfigureOllamaClient);

            serviceKey = $"{connectionName}_OllamaApiClient_internal";
            builder.Services.AddKeyedSingleton(serviceKey, (sp, _) => ConfigureOllamaClient(sp));
        }

        return new AspireOllamaApiClientBuilder(builder, serviceKey, settings.DisableTracing);

        IOllamaApiClient ConfigureOllamaClient(IServiceProvider serviceProvider)
        {
            HttpClient httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientKey);

            OllamaApiClient client = new(httpClient);
            if (!string.IsNullOrWhiteSpace(settings.SelectedModel))
            {
                client.SelectedModel = settings.SelectedModel;
                client.Config.Model = settings.SelectedModel;
            }

            return client;
        }
    }

    [Obsolete]
    private static IChatClient ConfigureOllamaChatClient(IServiceProvider serviceProvider, string serviceKey)
    {
        var ollamaClient = serviceProvider.GetRequiredKeyedService<IOllamaApiClient>(serviceKey);
        if (ollamaClient is IChatClient chatClient)
        {
            return chatClient;
        }

        throw new InvalidOperationException("The Ollama client does not implement IChatClient.");
    }

    [Obsolete]
    private static IEmbeddingGenerator<TInput, TEmbedding> ConfigureOllamaEmbeddingGenerator<TInput, TEmbedding>(IServiceProvider serviceProvider, string serviceKey)
        where TEmbedding : Embedding
    {
        var ollamaClient = serviceProvider.GetRequiredKeyedService<IOllamaApiClient>(serviceKey);
        if (ollamaClient is IEmbeddingGenerator<TInput, TEmbedding> embeddingGenerator)
        {
            return embeddingGenerator;
        }

        throw new InvalidOperationException("The Ollama client does not implement IEmbeddingGenerator<TInput, TEmbedding>.");
    }
}
