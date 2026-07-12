using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the ApplicationInfoResponseData payload.</summary>
public class ApplicationInfoResponseData
{
    /// <summary>Gets or sets <c>commit_id</c>.</summary>
    [JsonPropertyName("commit_id")]
    public string? CommitId { get; set; }

    /// <summary>Gets or sets <c>name</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets <c>openapi_docs</c>.</summary>
    [JsonPropertyName("openapi_docs")]
    public bool? OpenapiDocs { get; set; }

    /// <summary>Gets or sets <c>version</c>.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

}