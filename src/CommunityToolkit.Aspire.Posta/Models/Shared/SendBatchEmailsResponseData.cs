using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the SendBatchEmailsResponseData payload.</summary>
public class SendBatchEmailsResponseData
{
    /// <summary>Gets or sets <c>failed</c>.</summary>
    [JsonPropertyName("failed")]
    public int? Failed { get; set; }

    /// <summary>Gets or sets <c>results</c>.</summary>
    [JsonPropertyName("results")]
    public IReadOnlyList<SendBatchEmailsResponseDataResultsItem>? Results { get; set; }

    /// <summary>Gets or sets <c>sent</c>.</summary>
    [JsonPropertyName("sent")]
    public int? Sent { get; set; }

    /// <summary>Gets or sets <c>skipped</c>.</summary>
    [JsonPropertyName("skipped")]
    public int? Skipped { get; set; }

    /// <summary>Gets or sets <c>total</c>.</summary>
    [JsonPropertyName("total")]
    public int? Total { get; set; }

}