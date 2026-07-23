using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Emails;

/// <summary>Represents the VerifyAnEmailAddressResponse payload.</summary>
public class VerifyAnEmailAddressResponse
{
    /// <summary>Gets or sets <c>data</c>.</summary>
    [JsonPropertyName("data")]
    public Shared.VerifyAnEmailAddressResponseData? Data { get; set; }

    /// <summary>Gets or sets <c>success</c>.</summary>
    [JsonPropertyName("success")]
    public bool? Success { get; set; }

}