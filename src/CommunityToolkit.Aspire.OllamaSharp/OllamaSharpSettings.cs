namespace Aspire.CommunityToolkit.OllamaSharp;

/// <summary>
/// Represents the settings for OllamaSharp.
/// </summary>
public sealed class OllamaSharpSettings
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the selected model.
    /// </summary>
    public string? SelectedModel { get; set; }

    /// <summary>
    /// Gets or sets the list of models to available.
    /// </summary>
    public IReadOnlyList<string> Models { get; set; } = [];
}
