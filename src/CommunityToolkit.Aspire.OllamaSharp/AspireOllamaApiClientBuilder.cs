using OllamaSharp;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Builder class for configuring and creating an instance of AspireOllamaApiClient.
/// </summary>
/// <param name="hostBuilder">The <see cref="IHostApplicationBuilder"/> with which services are being registered.</param>
/// <param name="serviceKey">The service key used to register the <see cref="OllamaApiClient"/> service, if any.</param>
/// <param name="disableTracing">A flag to indicate whether tracing should be disabled.</param>
public class AspireOllamaApiClientBuilder(IHostApplicationBuilder hostBuilder, string serviceKey, bool disableTracing)
{
    /// <summary>
    /// The host application builder used to configure the application.
    /// </summary>
    public IHostApplicationBuilder HostBuilder { get; } = hostBuilder;

    /// <summary>
    /// Gets the service key used to register the <see cref="OllamaApiClient"/> service, if any.
    /// </summary>
    public string ServiceKey { get; } = serviceKey;

    /// <summary>
    /// Gets a flag indicating whether tracing should be disabled.
    /// </summary>
    public bool DisableTracing { get; } = disableTracing;
}