using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Templates;

/// <summary>Represents the UpdateTemplateResponse payload.</summary>
public class UpdateTemplateResponse
{
    /// <summary>Gets or sets <c>data</c>.</summary>
    [JsonPropertyName("data")]
    public Shared.UpdateTemplateResponseData? Data { get; set; }

    /// <summary>Gets or sets <c>success</c>.</summary>
    [JsonPropertyName("success")]
    public bool? Success { get; set; }

}