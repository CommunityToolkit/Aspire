using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the ReSubscribeAnEmailToAListResponseData payload.</summary>
public class ReSubscribeAnEmailToAListResponseData
{
    /// <summary>Gets or sets <c>action</c>.</summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    /// <summary>Gets or sets <c>email</c>.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    /// <summary>Gets or sets <c>list_created</c>.</summary>
    [JsonPropertyName("list_created")]
    public bool? ListCreated { get; set; }

    /// <summary>Gets or sets <c>list_id</c>.</summary>
    [JsonPropertyName("list_id")]
    public int? ListId { get; set; }

    /// <summary>Gets or sets <c>member_added</c>.</summary>
    [JsonPropertyName("member_added")]
    public bool? MemberAdded { get; set; }

    /// <summary>Gets or sets <c>subscriber_created</c>.</summary>
    [JsonPropertyName("subscriber_created")]
    public bool? SubscriberCreated { get; set; }

    /// <summary>Gets or sets <c>subscriber_id</c>.</summary>
    [JsonPropertyName("subscriber_id")]
    public int? SubscriberId { get; set; }

}