using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the SendBatchEmailsRequestUnsubscribe payload.</summary>
public class SendBatchEmailsRequestUnsubscribe
{
    /// <summary>Gets or sets <c>list_id</c>.</summary>
    [JsonPropertyName("list_id")]
    public int? ListId { get; set; }

    /// <summary>Gets or sets <c>mailto</c>.</summary>
    [JsonPropertyName("mailto")]
    public string? Mailto { get; set; }

    /// <summary>Gets or sets <c>one_click</c>.</summary>
    [JsonPropertyName("one_click")]
    public bool? OneClick { get; set; }

    /// <summary>Gets or sets <c>url</c>.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

}