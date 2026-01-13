namespace CommunityToolkit.Aspire.OllamaSharp;

/// <summary>
/// Represents the settings for OllamaSharp.
/// </summary>
public sealed class OllamaSharpSettings
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the selected model.
    /// </summary>
    public string? SelectedModel { get; set; }

    /// <summary>
    /// Gets or sets the list of models to available.
    /// </summary>
    public IReadOnlyList<string> Models { get; set; } = [];

    /// <summary>
    /// Gets or sets a boolean value that indicates whether the Ollama health check is disabled or not.
    /// </summary>
    /// <value>
    /// The default value is <see langword="false"/>.
    /// </value>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets a integer value that indicates the Ollama health check timeout in milliseconds.
    /// </summary>
    public int? HealthCheckTimeout { get; set; }

    /// <summary>
    /// Gets or sets a boolean value that indicates whether tracing is disabled or not.
    /// </summary>
    /// <remarks>Currently, the OllamaSharp SDK does not support tracing, but this is here for future use.</remarks>
    public bool DisableTracing { get; set; }

    /// <summary>
    /// Gets or sets the JSON serializer context for Native AOT support.
    /// </summary>
    /// <remarks>
    /// When using Native AOT, provide a custom <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> 
    /// that includes all types that will be serialized. This is required for AOT compatibility.
    /// See https://github.com/awaescher/OllamaSharp/blob/main/docs/native-aot-support.md for more information.
    /// </remarks>
    public System.Text.Json.Serialization.JsonSerializerContext? JsonSerializerContext { get; set; }

}
