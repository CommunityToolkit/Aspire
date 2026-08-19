using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Info;

/// <summary>Represents the ApplicationInfoResponse payload.</summary>
public class ApplicationInfoResponse
{
    /// <summary>Gets or sets <c>data</c>.</summary>
    [JsonPropertyName("data")]
    public Shared.ApplicationInfoResponseData? Data { get; set; }

    /// <summary>Gets or sets <c>success</c>.</summary>
    [JsonPropertyName("success")]
    public bool? Success { get; set; }

}