using Aspire;
using CommunityToolkit.Aspire.OllamaSharp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OllamaSharp;
using System.Security.Cryptography;

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
    [Obsolete("This method is obsolete. Use AddOllamaSharpClient instead.")]
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
    [Obsolete("This method is obsolete. Use AddKeyedOllamaSharpClient instead.")]
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
    /// <exception cref="UriFormatException">Thrown when no Ollama endpoint is provided.</exception>
    public static void AddOllamaSharpClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName, nameof(connectionName));
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        AddOllamaClientInternal(builder, DefaultConfigSectionName, connectionName, configureSettings: configureSettings, enableChatClient: true);
    }

    /// <summary>
    /// Adds <see cref="OllamaApiClient"/> and <see cref="IChatClient"/> services to the container using the <paramref name="connectionName"/> as the service key. 
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <exception cref="UriFormatException">Thrown when no Ollama endpoint is provided.</exception>
    public static void AddKeyedOllamaSharpClient(this IHostApplicationBuilder builder, string connectionName, Action<OllamaSharpSettings>? configureSettings = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName, nameof(connectionName));
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        AddOllamaClientInternal(builder, $"{DefaultConfigSectionName}:{connectionName}", connectionName, serviceKey: connectionName, configureSettings: configureSettings, enableChatClient: true);
    }

    private static void AddOllamaClientInternal(
        IHostApplicationBuilder builder,
        string configurationSectionName,
        string connectionName,
        string? serviceKey = null,
        Action<OllamaSharpSettings>? configureSettings = null,
        bool enableChatClient = false)
    {
        OllamaSharpSettings settings = new();
        builder.Configuration.GetSection(configurationSectionName).Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.Endpoint = connectionString;
        }

        configureSettings?.Invoke(settings);

        if (serviceKey is not null)
        {
            builder.Services.AddKeyedSingleton(serviceKey, (sp, _) => ConfigureOllamaClient(sp));
            if (enableChatClient)
            {
                builder.Services.AddKeyedSingleton(serviceKey, (sp, _) => ConfigureOllamaChatClient(sp, serviceKey));
            }
        }
        else
        {
            builder.Services.AddSingleton(ConfigureOllamaClient);
            if (enableChatClient)
            {
                builder.Services.AddSingleton(sp => ConfigureOllamaChatClient(sp));
            }
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
                var client = new OllamaApiClient(new HttpClient { BaseAddress = new Uri(settings.Endpoint) });
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

        IChatClient ConfigureOllamaChatClient(IServiceProvider serviceProvider, string? serviceKey = null)
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
    }
}