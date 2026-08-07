using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Emails;

/// <summary>Represents the SendAnEmailRequest payload.</summary>
public class SendAnEmailRequest
{
    /// <summary>Gets or sets <c>attachments</c>.</summary>
    [JsonPropertyName("attachments")]
    public IReadOnlyList<Shared.SendAnEmailRequestAttachmentsItem>? Attachments { get; set; }

    /// <summary>Gets or sets <c>from</c>.</summary>
    [JsonPropertyName("from")]
    public required string From { get; set; }

    /// <summary>Gets or sets <c>headers</c>.</summary>
    [JsonPropertyName("headers")]
    public IReadOnlyDictionary<string, JsonElement>? Headers { get; set; }

    /// <summary>Gets or sets <c>html</c>.</summary>
    [JsonPropertyName("html")]
    public string? Html { get; set; }

    /// <summary>Gets or sets <c>list_unsubscribe_post</c>.</summary>
    [JsonPropertyName("list_unsubscribe_post")]
    public bool? ListUnsubscribePost { get; set; }

    /// <summary>Gets or sets <c>list_unsubscribe_url</c>.</summary>
    [JsonPropertyName("list_unsubscribe_url")]
    public string? ListUnsubscribeUrl { get; set; }

    /// <summary>Gets or sets <c>send_at</c>.</summary>
    [JsonPropertyName("send_at")]
    public DateTimeOffset? SendAt { get; set; }

    /// <summary>Gets or sets <c>subject</c>.</summary>
    [JsonPropertyName("subject")]
    public required string Subject { get; set; }

    /// <summary>Gets or sets <c>text</c>.</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Gets or sets <c>to</c>.</summary>
    [JsonPropertyName("to")]
    public required IReadOnlyList<string> To { get; set; }

    /// <summary>Gets or sets <c>unsubscribe</c>.</summary>
    [JsonPropertyName("unsubscribe")]
    public Shared.SendAnEmailRequestUnsubscribe? Unsubscribe { get; set; }

    /// <summary>Gets or sets the <c>dry_run</c> query parameter.</summary>
    [JsonIgnore]
    public bool? DryRun { get; set; }

}