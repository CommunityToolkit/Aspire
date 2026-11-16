using CommunityToolkit.Aspire.VLLM;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// A builder for adding Microsoft.Extensions.AI clients that target a vLLM server.
/// </summary>
public sealed class AspireVLLMClientBuilder
{
    internal AspireVLLMClientBuilder(IHostApplicationBuilder hostBuilder, string connectionName, object? serviceKey, VLLMClientSettings settings)
    {
        HostBuilder = hostBuilder;
        ConnectionName = connectionName;
        ServiceKey = serviceKey;
        Settings = settings;
    }

    /// <summary>
    /// Gets the <see cref="IHostApplicationBuilder"/> with which services are being registered.
    /// </summary>
    public IHostApplicationBuilder HostBuilder { get; }

    /// <summary>
    /// Gets the name of the connection used to resolve the vLLM endpoint.
    /// </summary>
    public string ConnectionName { get; }

    /// <summary>
    /// Gets the service key used for keyed registrations, or <see langword="null"/> for unkeyed registrations.
    /// </summary>
    public object? ServiceKey { get; }

    /// <summary>
    /// Gets the resolved settings for the vLLM client.
    /// </summary>
    public VLLMClientSettings Settings { get; }

    /// <summary>
    /// Gets a flag indicating whether OpenTelemetry tracing is disabled.
    /// </summary>
    public bool DisableTracing => Settings.DisableTracing;
}
