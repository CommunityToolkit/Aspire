using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the GetTemplateResponseDataCreatedBy payload.</summary>
public class GetTemplateResponseDataCreatedBy
{
    /// <summary>Gets or sets <c>id</c>.</summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>Gets or sets <c>name</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

}