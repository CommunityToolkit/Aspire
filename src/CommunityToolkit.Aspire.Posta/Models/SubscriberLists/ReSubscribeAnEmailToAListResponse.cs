using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.SubscriberLists;

/// <summary>Represents the ReSubscribeAnEmailToAListResponse payload.</summary>
public class ReSubscribeAnEmailToAListResponse
{
    /// <summary>Gets or sets <c>data</c>.</summary>
    [JsonPropertyName("data")]
    public Shared.ReSubscribeAnEmailToAListResponseData? Data { get; set; }

    /// <summary>Gets or sets <c>success</c>.</summary>
    [JsonPropertyName("success")]
    public bool? Success { get; set; }

}