using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Templates;

/// <summary>Represents the PreviewTemplateRequest payload.</summary>
public class PreviewTemplateRequest
{
    /// <summary>Gets or sets <c>html_template</c>.</summary>
    [JsonPropertyName("html_template")]
    public string? HtmlTemplate { get; set; }

    /// <summary>Gets or sets <c>stylesheet_id</c>.</summary>
    [JsonPropertyName("stylesheet_id")]
    public int? StylesheetId { get; set; }

    /// <summary>Gets or sets <c>subject_template</c>.</summary>
    [JsonPropertyName("subject_template")]
    public required string SubjectTemplate { get; set; }

    /// <summary>Gets or sets <c>template_data</c>.</summary>
    [JsonPropertyName("template_data")]
    public IReadOnlyDictionary<string, JsonElement>? TemplateData { get; set; }

    /// <summary>Gets or sets <c>text_template</c>.</summary>
    [JsonPropertyName("text_template")]
    public string? TextTemplate { get; set; }

}