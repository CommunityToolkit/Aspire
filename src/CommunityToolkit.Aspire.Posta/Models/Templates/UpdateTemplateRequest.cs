using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Templates;

/// <summary>Represents the UpdateTemplateRequest payload.</summary>
public class UpdateTemplateRequest
{
    /// <summary>Gets or sets <c>default_language</c>.</summary>
    [JsonPropertyName("default_language")]
    public string? DefaultLanguage { get; set; }

    /// <summary>Gets or sets <c>description</c>.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets <c>name</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Gets or sets <c>sample_data</c>.</summary>
    [JsonPropertyName("sample_data")]
    public string? SampleData { get; set; }

    /// <summary>Gets or sets the <c>id</c> path parameter.</summary>
    [JsonIgnore]
    public required int Id { get; set; }

}