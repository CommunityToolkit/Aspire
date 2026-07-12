using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the SendAnEmailRequestAttachmentsItem payload.</summary>
public class SendAnEmailRequestAttachmentsItem
{
    /// <summary>Gets or sets <c>content</c>.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    /// <summary>Gets or sets <c>content_type</c>.</summary>
    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    /// <summary>Gets or sets <c>filename</c>.</summary>
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    /// <summary>Gets or sets <c>storage_key</c>.</summary>
    [JsonPropertyName("storage_key")]
    public string? StorageKey { get; set; }

}