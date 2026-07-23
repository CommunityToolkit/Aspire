using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the VerifyAnEmailAddressResponseDataChecks payload.</summary>
public class VerifyAnEmailAddressResponseDataChecks
{
    /// <summary>Gets or sets <c>disposable</c>.</summary>
    [JsonPropertyName("disposable")]
    public bool? Disposable { get; set; }

    /// <summary>Gets or sets <c>mx</c>.</summary>
    [JsonPropertyName("mx")]
    public bool? Mx { get; set; }

    /// <summary>Gets or sets <c>role_account</c>.</summary>
    [JsonPropertyName("role_account")]
    public bool? RoleAccount { get; set; }

    /// <summary>Gets or sets <c>smtp</c>.</summary>
    [JsonPropertyName("smtp")]
    public string? Smtp { get; set; }

    /// <summary>Gets or sets <c>syntax</c>.</summary>
    [JsonPropertyName("syntax")]
    public bool? Syntax { get; set; }

}