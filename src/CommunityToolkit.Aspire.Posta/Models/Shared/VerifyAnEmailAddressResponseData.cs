using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the VerifyAnEmailAddressResponseData payload.</summary>
public class VerifyAnEmailAddressResponseData
{
    /// <summary>Gets or sets <c>cached</c>.</summary>
    [JsonPropertyName("cached")]
    public bool? Cached { get; set; }

    /// <summary>Gets or sets <c>checked_at</c>.</summary>
    [JsonPropertyName("checked_at")]
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>Gets or sets <c>checks</c>.</summary>
    [JsonPropertyName("checks")]
    public VerifyAnEmailAddressResponseDataChecks? Checks { get; set; }

    /// <summary>Gets or sets <c>email</c>.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>Gets or sets <c>mailbox_verified</c>.</summary>
    [JsonPropertyName("mailbox_verified")]
    public bool? MailboxVerified { get; set; }

    /// <summary>Gets or sets <c>previously_bounced</c>.</summary>
    [JsonPropertyName("previously_bounced")]
    public bool? PreviouslyBounced { get; set; }

    /// <summary>Gets or sets <c>reason</c>.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>Gets or sets <c>score</c>.</summary>
    [JsonPropertyName("score")]
    public int? Score { get; set; }

    /// <summary>Gets or sets <c>status</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Gets or sets <c>suppressed</c>.</summary>
    [JsonPropertyName("suppressed")]
    public bool? Suppressed { get; set; }

}