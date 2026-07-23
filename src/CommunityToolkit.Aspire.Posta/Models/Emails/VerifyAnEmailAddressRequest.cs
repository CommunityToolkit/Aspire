using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Emails;

/// <summary>Represents the VerifyAnEmailAddressRequest payload.</summary>
public class VerifyAnEmailAddressRequest
{
    /// <summary>Gets or sets <c>email</c>.</summary>
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    /// <summary>Gets or sets the <c>fresh</c> query parameter.</summary>
    [JsonIgnore]
    public bool? Fresh { get; set; }

}