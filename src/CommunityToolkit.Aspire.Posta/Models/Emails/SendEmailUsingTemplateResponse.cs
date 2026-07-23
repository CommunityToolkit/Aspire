using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Emails;

/// <summary>Represents the SendEmailUsingTemplateResponse payload.</summary>
public class SendEmailUsingTemplateResponse
{
    /// <summary>Gets or sets <c>data</c>.</summary>
    [JsonPropertyName("data")]
    public Shared.SendEmailUsingTemplateResponseData? Data { get; set; }

    /// <summary>Gets or sets <c>success</c>.</summary>
    [JsonPropertyName("success")]
    public bool? Success { get; set; }

}