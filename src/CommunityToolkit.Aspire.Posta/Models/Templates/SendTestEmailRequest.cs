using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Templates;

/// <summary>Represents the SendTestEmailRequest payload.</summary>
public class SendTestEmailRequest
{
    /// <summary>Gets or sets <c>from</c>.</summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }

    /// <summary>Gets or sets <c>language</c>.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>Gets or sets <c>template_data</c>.</summary>
    [JsonPropertyName("template_data")]
    public IReadOnlyDictionary<string, JsonElement>? TemplateData { get; set; }

    /// <summary>Gets or sets <c>to</c>.</summary>
    [JsonPropertyName("to")]
    public required IReadOnlyList<string> To { get; set; }

    /// <summary>Gets or sets the <c>id</c> path parameter.</summary>
    [JsonIgnore]
    public required int Id { get; set; }

}