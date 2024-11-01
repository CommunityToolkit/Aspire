using Aspire;
using CommunityToolkit.Aspire.OllamaSharp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OllamaSharp;
using System.Data.Common;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for setting up OllamaSharp client in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireOllamaSharpExtensions
{
    private const string DefaultConfigSectionName = "Aspire:OllamaSharp";

    /// <summary>
    /// Adds <see cref="OllamaApiClient"/> services to the container.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="UriFormatException">Thrown when no Ollama endpoint is provided.</exception>
    public static void AddOllamaApiClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName, nameof(connectionName));
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        AddOllamaClientInternal(builder, DefaultConfigSectionName, connectionName, configureSettings: configureSettings);
    }

    /// <summary>
    /// Adds <see cref="OllamaApiClient"/> services to the container using the <paramref name="connectionName"/> as the service key. 
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="UriFormatException">Thrown when no Ollama endpoint is provided.</exception>
    public static void AddKeyedOllamaApiClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName, nameof(connectionName));
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        AddOllamaClientInternal(builder, $"{DefaultConfigSectionName}:{connectionName}", connectionName, serviceKey: connectionName, configureSettings: configureSettings);
    }

    /// <summary>
    /// Adds <see cref="OllamaApiClient"/> and <see cref="IChatClient"/> services to the container.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when no Ollama endpoint is provided.</exception>
    public static void AddOllamaSharpChatClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        builder.AddOllamaApiClient(connectionName, configureSettings);
        builder.Services.AddSingleton(sp => ConfigureOllamaChatClient(sp));
    }

    /// <summary>
    /// Adds <see cref="OllamaApiClient"/> and <see cref="IChatClient"/> services to the container using the <paramref name="connectionName"/> as the service key. 
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when no Ollama endpoint is provided.</exception>
    public static void AddKeyedOllamaSharpChatClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        builder.AddKeyedOllamaApiClient(connectionName, configureSettings);
        builder.Services.AddKeyedSingleton(connectionName, (sp, _) => ConfigureOllamaChatClient(sp, connectionName));
    }

    /// <summary>
    /// Adds <see cref="OllamaApiClient"/> and <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> services to the container.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when no Ollama endpoint is provided.</exception>
    public static void AddOllamaSharpEmbeddingGenerator(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        builder.AddOllamaApiClient(connectionName, configureSettings);
        builder.Services.AddSingleton(sp => ConfigureOllamaEmbeddingGenerator<string, Embedding<float>>(sp));
    }

    /// <summary>
    /// Adds <see cref="OllamaApiClient"/> and <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> services to the container using the <paramref name="connectionName"/> as the service key.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when no Ollama endpoint is provided.</exception>
    public static void AddKeyedOllamaSharpEmbeddingGenerator(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        builder.AddKeyedOllamaApiClient(connectionName, configureSettings);
        builder.Services.AddKeyedSingleton(connectionName, (sp, _) => ConfigureOllamaEmbeddingGenerator<string, Embedding<float>>(sp, connectionName));
    }

    private static void AddOllamaClientInternal(
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

        if (serviceKey is not null)
        {
            builder.Services.AddKeyedSingleton(serviceKey, (sp, _) => ConfigureOllamaClient(sp));
        }
        else
        {
            builder.Services.AddSingleton(ConfigureOllamaClient);
        }

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = serviceKey is null ? "OllamaSharp" : $"OllamaSharp_{connectionName}";

            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => new OllamaHealthCheck(serviceKey is null ?
                    sp.GetRequiredService<IOllamaApiClient>() :
                    sp.GetRequiredKeyedService<IOllamaApiClient>(serviceKey)),
                failureStatus: null,
                tags: null,
                timeout: settings.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : null
                ));
        }

        IOllamaApiClient ConfigureOllamaClient(IServiceProvider serviceProvider)
        {
            if (settings.Endpoint is not null)
            {
                var client = new OllamaApiClient(new HttpClient { BaseAddress = settings.Endpoint });
                if (!string.IsNullOrWhiteSpace(settings.SelectedModel))
                {
                    client.SelectedModel = settings.SelectedModel;
                }

                return client;
            }

            throw new InvalidOperationException(
                        $"An OllamaApiClient could not be configured. Ensure valid connection information was provided in 'ConnectionStrings:{connectionName}' or either " +
                        $"{nameof(settings.Endpoint)} must be provided " +
                        $"in the '{configurationSectionName}' configuration section.");
        }
    }

    private static IChatClient ConfigureOllamaChatClient(IServiceProvider serviceProvider, string? serviceKey = null)
    {
        var ollamaClient = serviceKey is null ?
            serviceProvider.GetRequiredService<IOllamaApiClient>() :
            serviceProvider.GetRequiredKeyedService<IOllamaApiClient>(serviceKey);
        if (ollamaClient is IChatClient chatClient)
        {
            return chatClient;
        }

        throw new InvalidOperationException("The Ollama client does not implement IChatClient.");
    }

    private static IEmbeddingGenerator<TInput, TEmbedding> ConfigureOllamaEmbeddingGenerator<TInput, TEmbedding>(IServiceProvider serviceProvider, string? serviceKey = null)
        where TEmbedding : Embedding
    {
        var ollamaClient = serviceKey is null ?
            serviceProvider.GetRequiredService<IOllamaApiClient>() :
            serviceProvider.GetRequiredKeyedService<IOllamaApiClient>(serviceKey);
        if (ollamaClient is IEmbeddingGenerator<TInput, TEmbedding> embeddingGenerator)
        {
            return embeddingGenerator;
        }

        throw new InvalidOperationException("The Ollama client does not implement IEmbeddingGenerator<TInput, TEmbedding>.");
    }
}