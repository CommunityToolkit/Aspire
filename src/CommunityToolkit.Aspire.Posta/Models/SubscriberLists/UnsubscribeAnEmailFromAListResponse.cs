using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.SubscriberLists;

/// <summary>Represents the UnsubscribeAnEmailFromAListResponse payload.</summary>
public class UnsubscribeAnEmailFromAListResponse
{
    /// <summary>Gets or sets <c>data</c>.</summary>
    [JsonPropertyName("data")]
    public Shared.UnsubscribeAnEmailFromAListResponseData? Data { get; set; }

    /// <summary>Gets or sets <c>success</c>.</summary>
    [JsonPropertyName("success")]
    public bool? Success { get; set; }

}