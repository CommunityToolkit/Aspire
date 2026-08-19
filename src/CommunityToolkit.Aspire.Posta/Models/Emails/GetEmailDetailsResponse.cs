using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Emails;

/// <summary>Represents the GetEmailDetailsResponse payload.</summary>
public class GetEmailDetailsResponse
{
    /// <summary>Gets or sets <c>data</c>.</summary>
    [JsonPropertyName("data")]
    public Shared.GetEmailDetailsResponseData? Data { get; set; }

    /// <summary>Gets or sets <c>success</c>.</summary>
    [JsonPropertyName("success")]
    public bool? Success { get; set; }

}