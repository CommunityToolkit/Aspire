using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the SendAnEmailResponseData payload.</summary>
public class SendAnEmailResponseData
{
    /// <summary>Gets or sets <c>id</c>.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets <c>status</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

}