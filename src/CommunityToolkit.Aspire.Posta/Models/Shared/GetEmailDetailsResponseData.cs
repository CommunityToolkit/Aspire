using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Shared;

/// <summary>Represents the GetEmailDetailsResponseData payload.</summary>
public class GetEmailDetailsResponseData
{
    /// <summary>Gets or sets <c>api_key_id</c>.</summary>
    [JsonPropertyName("api_key_id")]
    public int? ApiKeyId { get; set; }

    /// <summary>Gets or sets <c>api_key_name</c>.</summary>
    [JsonPropertyName("api_key_name")]
    public string? ApiKeyName { get; set; }

    /// <summary>Gets or sets <c>attachments_json</c>.</summary>
    [JsonPropertyName("attachments_json")]
    public string? AttachmentsJson { get; set; }

    /// <summary>Gets or sets <c>created_at</c>.</summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Gets or sets <c>error_message</c>.</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets <c>headers_json</c>.</summary>
    [JsonPropertyName("headers_json")]
    public string? HeadersJson { get; set; }

    /// <summary>Gets or sets <c>html_body</c>.</summary>
    [JsonPropertyName("html_body")]
    public string? HtmlBody { get; set; }

    /// <summary>Gets or sets <c>id</c>.</summary>
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    /// <summary>Gets or sets <c>list_unsubscribe_mailto</c>.</summary>
    [JsonPropertyName("list_unsubscribe_mailto")]
    public string? ListUnsubscribeMailto { get; set; }

    /// <summary>Gets or sets <c>list_unsubscribe_post</c>.</summary>
    [JsonPropertyName("list_unsubscribe_post")]
    public bool? ListUnsubscribePost { get; set; }

    /// <summary>Gets or sets <c>list_unsubscribe_url</c>.</summary>
    [JsonPropertyName("list_unsubscribe_url")]
    public string? ListUnsubscribeUrl { get; set; }

    /// <summary>Gets or sets <c>provider</c>.</summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    /// <summary>Gets or sets <c>recipients</c>.</summary>
    [JsonPropertyName("recipients")]
    public IReadOnlyList<string>? Recipients { get; set; }

    /// <summary>Gets or sets <c>retry_count</c>.</summary>
    [JsonPropertyName("retry_count")]
    public int? RetryCount { get; set; }

    /// <summary>Gets or sets <c>scheduled_at</c>.</summary>
    [JsonPropertyName("scheduled_at")]
    public DateTimeOffset? ScheduledAt { get; set; }

    /// <summary>Gets or sets <c>sender</c>.</summary>
    [JsonPropertyName("sender")]
    public string? Sender { get; set; }

    /// <summary>Gets or sets <c>sent_at</c>.</summary>
    [JsonPropertyName("sent_at")]
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Gets or sets <c>smtp_hostname</c>.</summary>
    [JsonPropertyName("smtp_hostname")]
    public string? SmtpHostname { get; set; }

    /// <summary>Gets or sets <c>status</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Gets or sets <c>subject</c>.</summary>
    [JsonPropertyName("subject")]
    public string? Subject { get; set; }

    /// <summary>Gets or sets <c>template_name</c>.</summary>
    [JsonPropertyName("template_name")]
    public string? TemplateName { get; set; }

    /// <summary>Gets or sets <c>text_body</c>.</summary>
    [JsonPropertyName("text_body")]
    public string? TextBody { get; set; }

    /// <summary>Gets or sets <c>unsubscribe_list_id</c>.</summary>
    [JsonPropertyName("unsubscribe_list_id")]
    public int? UnsubscribeListId { get; set; }

    /// <summary>Gets or sets <c>user_id</c>.</summary>
    [JsonPropertyName("user_id")]
    public int? UserId { get; set; }

    /// <summary>Gets or sets <c>uuid</c>.</summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; set; }

    /// <summary>Gets or sets <c>workspace_id</c>.</summary>
    [JsonPropertyName("workspace_id")]
    public int? WorkspaceId { get; set; }

}