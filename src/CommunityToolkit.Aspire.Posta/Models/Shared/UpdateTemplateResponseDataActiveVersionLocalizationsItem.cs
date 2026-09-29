using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the UpdateTemplateResponseDataActiveVersionLocalizationsItem payload.</summary>
public class UpdateTemplateResponseDataActiveVersionLocalizationsItem
{
    /// <summary>Gets or sets <c>builder_json</c>.</summary>
    [JsonPropertyName("builder_json")]
    public string? BuilderJson { get; set; }

    /// <summary>Gets or sets <c>created_at</c>.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Gets or sets <c>html_template</c>.</summary>
    [JsonPropertyName("html_template")]
    public string? HtmlTemplate { get; set; }

    /// <summary>Gets or sets <c>id</c>.</summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>Gets or sets <c>language</c>.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>Gets or sets <c>subject_template</c>.</summary>
    [JsonPropertyName("subject_template")]
    public string? SubjectTemplate { get; set; }

    /// <summary>Gets or sets <c>text_template</c>.</summary>
    [JsonPropertyName("text_template")]
    public string? TextTemplate { get; set; }

    /// <summary>Gets or sets <c>updated_at</c>.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Gets or sets <c>version_id</c>.</summary>
    [JsonPropertyName("version_id")]
    public int? VersionId { get; set; }

}