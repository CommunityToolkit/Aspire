using Aspire;
using CommunityToolkit.Aspire.VLLM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for registering a vLLM client in an <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class AspireVLLMExtensions
{
    /// <summary>
    /// The configuration section used for shared and per-connection vLLM settings.
    /// </summary>
    internal const string DefaultConfigSectionName = "Aspire:VLLM";

    /// <summary>
    /// Registers a vLLM client for the specified connection. Call <see cref="AspireVLLMChatClientExtensions.AddChatClient(AspireVLLMClientBuilder, string?)"/>
    /// on the returned builder to register an <see cref="Microsoft.Extensions.AI.IChatClient"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <returns>An <see cref="AspireVLLMClientBuilder"/> that can be used to register AI clients.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no vLLM endpoint is provided.</exception>
    public static AspireVLLMClientBuilder AddVLLMClient(this IHostApplicationBuilder builder, string connectionName, Action<VLLMClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        return AddVLLMClientInternal(builder, connectionName, serviceKey: null, configureSettings);
    }

    /// <summary>
    /// Registers a keyed vLLM client for the specified connection, using <paramref name="connectionName"/> as the service key.
    /// Call <see cref="AspireVLLMChatClientExtensions.AddKeyedChatClient(AspireVLLMClientBuilder, string?)"/> on the returned builder
    /// to register a keyed <see cref="Microsoft.Extensions.AI.IChatClient"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <returns>An <see cref="AspireVLLMClientBuilder"/> that can be used to register AI clients.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no vLLM endpoint is provided.</exception>
    public static AspireVLLMClientBuilder AddKeyedVLLMClient(this IHostApplicationBuilder builder, string connectionName, Action<VLLMClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        return AddVLLMClientInternal(builder, connectionName, serviceKey: connectionName, configureSettings);
    }

    /// <summary>
    /// Registers a keyed vLLM client for the specified connection, using the specified <paramref name="serviceKey"/>.
    /// Call <see cref="AspireVLLMChatClientExtensions.AddKeyedChatClient(AspireVLLMClientBuilder, string?)"/> on the returned builder
    /// to register a keyed <see cref="Microsoft.Extensions.AI.IChatClient"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="serviceKey">A unique key that identifies this instance of the vLLM client.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <returns>An <see cref="AspireVLLMClientBuilder"/> that can be used to register AI clients.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no vLLM endpoint is provided.</exception>
    public static AspireVLLMClientBuilder AddKeyedVLLMClient(this IHostApplicationBuilder builder, object serviceKey, string connectionName, Action<VLLMClientSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        return AddVLLMClientInternal(builder, connectionName, serviceKey, configureSettings);
    }

    private static AspireVLLMClientBuilder AddVLLMClientInternal(
        IHostApplicationBuilder builder,
        string connectionName,
        object? serviceKey,
        Action<VLLMClientSettings>? configureSettings)
    {
        var settings = new VLLMClientSettings();
        var configSection = builder.Configuration.GetSection(DefaultConfigSectionName);
        configSection.Bind(settings);
        configSection.GetSection(connectionName).Bind(settings);
        settings.ParseConnectionString(builder.Configuration.GetConnectionString(connectionName));
        configureSettings?.Invoke(settings);

        if (settings.Endpoint is null)
        {
            throw new InvalidOperationException(
                $"A vLLM client could not be configured. Ensure valid connection information was provided in 'ConnectionStrings:{connectionName}' or " +
                $"{nameof(VLLMClientSettings.Endpoint)} must be provided in the '{DefaultConfigSectionName}:{connectionName}' configuration section.");
        }

        if (!settings.DisableHealthChecks)
        {
            var healthCheckName = serviceKey is null ? "VLLM" : $"VLLM_{connectionName}";
            var httpClientName = $"{connectionName}_vllm_healthcheck";
            var endpoint = settings.Endpoint;

            builder.Services.AddHttpClient(httpClientName);
            builder.TryAddHealthCheck(new HealthCheckRegistration(
                healthCheckName,
                sp => new VLLMHealthCheck(sp.GetRequiredService<IHttpClientFactory>().CreateClient(httpClientName), endpoint),
                failureStatus: null,
                tags: null,
                timeout: settings.HealthCheckTimeout > 0 ? TimeSpan.FromMilliseconds(settings.HealthCheckTimeout.Value) : null));
        }

        return new AspireVLLMClientBuilder(builder, connectionName, serviceKey, settings);
    }
}
