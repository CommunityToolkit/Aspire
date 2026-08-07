using CommunityToolkit.Aspire.Posta.Clients;
using CommunityToolkit.Aspire.Posta.Configuration;
using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Transport;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.Hosting;
#pragma warning restore IDE0130

/// <summary>Extension methods for registering a Posta API client.</summary>
public static class AspirePostaExtensions
{
    private const string DefaultConfigurationSection = "Aspire:Posta:Client";

    /// <summary>Registers an <see cref="IPostaClient"/> using Aspire service discovery or a configured endpoint.</summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="connectionName">The Posta resource or connection string name.</param>
    /// <param name="configureSettings">An optional settings callback applied last.</param>
    /// <param name="configurationSectionName">An optional configuration section name.</param>
    public static void AddPostaClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<PostaClientSettings>? configureSettings = null,
        string? configurationSectionName = null)
    {
        AddPostaClientCore(builder, serviceKey: null, connectionName, configureSettings, configurationSectionName);
    }

    /// <summary>Registers a keyed <see cref="IPostaClient"/> whose service key is the connection name.</summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="connectionName">The Posta resource or connection string name and service key.</param>
    /// <param name="configureSettings">An optional settings callback applied last.</param>
    /// <param name="configurationSectionName">An optional configuration section name.</param>
    public static void AddKeyedPostaClient(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<PostaClientSettings>? configureSettings = null,
        string? configurationSectionName = null)
    {
        AddPostaClientCore(builder, connectionName, connectionName, configureSettings, configurationSectionName);
    }

    /// <summary>Registers a keyed <see cref="IPostaClient"/> using a custom service key.</summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="serviceKey">The dependency injection service key.</param>
    /// <param name="connectionName">The Posta resource or connection string name.</param>
    /// <param name="configureSettings">An optional settings callback applied last.</param>
    /// <param name="configurationSectionName">An optional configuration section name.</param>
    public static void AddKeyedPostaClient(
        this IHostApplicationBuilder builder,
        object serviceKey,
        string connectionName,
        Action<PostaClientSettings>? configureSettings = null,
        string? configurationSectionName = null)
    {
        ArgumentNullException.ThrowIfNull(serviceKey);
        AddPostaClientCore(builder, serviceKey, connectionName, configureSettings, configurationSectionName);
    }

    private static void AddPostaClientCore(
        IHostApplicationBuilder builder,
        object? serviceKey,
        string connectionName,
        Action<PostaClientSettings>? configureSettings,
        string? configurationSectionName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        PostaClientSettings settings = new();
        string sectionName = configurationSectionName ?? DefaultConfigurationSection;
        builder.Configuration.GetSection(sectionName).Bind(settings);
        builder.Configuration.GetSection($"{sectionName}:{connectionName}").Bind(settings);
        ApplyConnectionString(settings, builder.Configuration.GetConnectionString(connectionName));
        configureSettings?.Invoke(settings);

        Uri endpoint = settings.Endpoint ?? new Uri($"http://{connectionName}", UriKind.Absolute);
        builder.Services.AddHttpClient($"Posta:{connectionName}", client =>
        {
            client.BaseAddress = endpoint;
            client.Timeout = settings.Timeout;
        }).AddServiceDiscovery();

        builder.Services.TryAddSingleton<IPostaEndpoints, PostaEndpoints>();
        IPostaClient CreateClient(IServiceProvider services)
        {
            IPostaCredentialProvider credentialProvider = services.GetService<IPostaCredentialProvider>() ?? new PostaCredentialProvider(settings);
            HttpClient httpClient = services.GetRequiredService<IHttpClientFactory>().CreateClient($"Posta:{connectionName}");
            return new PostaClient(new PostaTransport(httpClient, credentialProvider), services.GetRequiredService<IPostaEndpoints>());
        }

        if (serviceKey is null)
        {
            builder.Services.AddSingleton<IPostaClient>(CreateClient);
        }
        else
        {
            builder.Services.AddKeyedSingleton<IPostaClient>(serviceKey, (services, _) => CreateClient(services));
        }
    }

    private static void ApplyConnectionString(PostaClientSettings settings, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out Uri? uri))
        {
            settings.Endpoint = uri;
            return;
        }

        DbConnectionStringBuilder connectionStringBuilder = new()
        {
            ConnectionString = connectionString
        };

        if (connectionStringBuilder.TryGetValue("Endpoint", out object? endpoint) &&
            Uri.TryCreate(endpoint.ToString(), UriKind.Absolute, out uri))
        {
            settings.Endpoint = uri;
        }
        if (connectionStringBuilder.TryGetValue("ApiKey", out object? apiKey))
        {
            settings.ApiKey = apiKey.ToString();
        }
        if (connectionStringBuilder.TryGetValue("AccessToken", out object? accessToken))
        {
            settings.AccessToken = accessToken.ToString();
        }
    }
}
