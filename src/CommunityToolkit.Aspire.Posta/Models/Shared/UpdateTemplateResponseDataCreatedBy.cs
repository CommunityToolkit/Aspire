using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the UpdateTemplateResponseDataCreatedBy payload.</summary>
public class UpdateTemplateResponseDataCreatedBy
{
    /// <summary>Gets or sets <c>id</c>.</summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>Gets or sets <c>name</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

}