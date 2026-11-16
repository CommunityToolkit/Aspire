using System.Data.Common;

namespace CommunityToolkit.Aspire.VLLM;

/// <summary>
/// Provides the client configuration settings for connecting to a vLLM OpenAI-compatible inference server.
/// </summary>
public sealed class VLLMClientSettings
{
    /// <summary>
    /// A non-secret placeholder required by the OpenAI client when vLLM authentication is not configured.
    /// </summary>
    internal const string PlaceholderApiKey = "not-used";

    /// <summary>
    /// Gets or sets the base endpoint of the vLLM server, for example <c>http://localhost:8000</c>.
    /// The <c>/v1</c> OpenAI base path is appended automatically.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the API key. This is only required when the vLLM server was started with <c>--api-key</c>.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the served model name to use for chat requests.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the vLLM health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets a integer value that indicates the vLLM health check timeout in milliseconds.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the OpenTelemetry tracing is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableTracing { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether prompts and responses are included in telemetry.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool EnableSensitiveTelemetryData { get; set; }

    /// <summary>
    /// Applies a base HTTP URI or the endpoint, key, and model values from a connection string.
    /// </summary>
    /// <param name="connectionString">The connection string, or <see langword="null"/> to leave the settings unchanged.</param>
    internal void ParseConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            Endpoint = uri;
            return;
        }

        var connectionBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

        if (connectionBuilder.TryGetValue("Endpoint", out var endpoint)
            && Uri.TryCreate(endpoint?.ToString(), UriKind.Absolute, out var endpointUri))
        {
            Endpoint = endpointUri;
        }

        if (connectionBuilder.TryGetValue("Key", out var key) && key?.ToString() is { Length: > 0 } keyValue)
        {
            Key = keyValue;
        }

        if (connectionBuilder.TryGetValue("Model", out var model) && model?.ToString() is { Length: > 0 } modelValue)
        {
            Model = modelValue;
        }
    }
}
