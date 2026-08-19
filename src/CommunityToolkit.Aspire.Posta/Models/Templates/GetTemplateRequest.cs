using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Templates;

/// <summary>Represents the GetTemplateRequest payload.</summary>
public class GetTemplateRequest
{
    /// <summary>Gets or sets the <c>id</c> path parameter.</summary>
    [JsonIgnore]
    public required int Id { get; set; }

}