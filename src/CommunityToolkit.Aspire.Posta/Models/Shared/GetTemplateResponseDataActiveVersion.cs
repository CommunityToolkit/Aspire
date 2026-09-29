using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the GetTemplateResponseDataActiveVersion payload.</summary>
public class GetTemplateResponseDataActiveVersion
{
    /// <summary>Gets or sets <c>created_at</c>.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Gets or sets <c>id</c>.</summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>Gets or sets <c>localizations</c>.</summary>
    [JsonPropertyName("localizations")]
    public IReadOnlyList<GetTemplateResponseDataActiveVersionLocalizationsItem>? Localizations { get; set; }

    /// <summary>Gets or sets <c>sample_data</c>.</summary>
    [JsonPropertyName("sample_data")]
    public string? SampleData { get; set; }

    /// <summary>Gets or sets <c>stylesheet</c>.</summary>
    [JsonPropertyName("stylesheet")]
    public GetTemplateResponseDataActiveVersionStylesheet? Stylesheet { get; set; }

    /// <summary>Gets or sets <c>stylesheet_id</c>.</summary>
    [JsonPropertyName("stylesheet_id")]
    public int? StylesheetId { get; set; }

    /// <summary>Gets or sets <c>template_id</c>.</summary>
    [JsonPropertyName("template_id")]
    public int? TemplateId { get; set; }

    /// <summary>Gets or sets <c>version</c>.</summary>
    [JsonPropertyName("version")]
    public int? Version { get; set; }

}