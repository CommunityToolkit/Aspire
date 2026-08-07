using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the CreateTemplateResponseData payload.</summary>
public class CreateTemplateResponseData
{
    /// <summary>Gets or sets <c>active_version</c>.</summary>
    [JsonPropertyName("active_version")]
    public CreateTemplateResponseDataActiveVersion? ActiveVersion { get; set; }

    /// <summary>Gets or sets <c>active_version_id</c>.</summary>
    [JsonPropertyName("active_version_id")]
    public int? ActiveVersionId { get; set; }

    /// <summary>Gets or sets <c>created_at</c>.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Gets or sets <c>created_by</c>.</summary>
    [JsonPropertyName("created_by")]
    public CreateTemplateResponseDataCreatedBy? CreatedBy { get; set; }

    /// <summary>Gets or sets <c>default_language</c>.</summary>
    [JsonPropertyName("default_language")]
    public string? DefaultLanguage { get; set; }

    /// <summary>Gets or sets <c>description</c>.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets <c>id</c>.</summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>Gets or sets <c>last_edited_by</c>.</summary>
    [JsonPropertyName("last_edited_by")]
    public CreateTemplateResponseDataLastEditedBy? LastEditedBy { get; set; }

    /// <summary>Gets or sets <c>last_edited_by_id</c>.</summary>
    [JsonPropertyName("last_edited_by_id")]
    public int? LastEditedById { get; set; }

    /// <summary>Gets or sets <c>name</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets <c>sample_data</c>.</summary>
    [JsonPropertyName("sample_data")]
    public string? SampleData { get; set; }

    /// <summary>Gets or sets <c>updated_at</c>.</summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Gets or sets <c>user_id</c>.</summary>
    [JsonPropertyName("user_id")]
    public int? UserId { get; set; }

    /// <summary>Gets or sets <c>workspace_id</c>.</summary>
    [JsonPropertyName("workspace_id")]
    public int? WorkspaceId { get; set; }

}