using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Emails;

/// <summary>Represents the SendBatchEmailsRequest payload.</summary>
public class SendBatchEmailsRequest
{
    /// <summary>Gets or sets <c>from</c>.</summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }

    /// <summary>Gets or sets <c>language</c>.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>Gets or sets <c>recipients</c>.</summary>
    [JsonPropertyName("recipients")]
    public required IReadOnlyList<Shared.SendBatchEmailsRequestRecipientsItem> Recipients { get; set; }

    /// <summary>Gets or sets <c>template</c>.</summary>
    [JsonPropertyName("template")]
    public string? Template { get; set; }

    /// <summary>Gets or sets <c>template_id</c>.</summary>
    [JsonPropertyName("template_id")]
    public int? TemplateId { get; set; }

    /// <summary>Gets or sets <c>unsubscribe</c>.</summary>
    [JsonPropertyName("unsubscribe")]
    public Shared.SendBatchEmailsRequestUnsubscribe? Unsubscribe { get; set; }

    /// <summary>Gets or sets the <c>dry_run</c> query parameter.</summary>
    [JsonIgnore]
    public bool? DryRun { get; set; }

}