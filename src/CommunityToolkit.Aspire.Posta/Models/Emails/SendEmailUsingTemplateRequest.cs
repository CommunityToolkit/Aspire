using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Emails;

/// <summary>Represents the SendEmailUsingTemplateRequest payload.</summary>
public class SendEmailUsingTemplateRequest
{
    /// <summary>Gets or sets <c>attachments</c>.</summary>
    [JsonPropertyName("attachments")]
    public IReadOnlyList<Shared.SendEmailUsingTemplateRequestAttachmentsItem>? Attachments { get; set; }

    /// <summary>Gets or sets <c>from</c>.</summary>
    [JsonPropertyName("from")]
    public string? From { get; set; }

    /// <summary>Gets or sets <c>language</c>.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>Gets or sets <c>template</c>.</summary>
    [JsonPropertyName("template")]
    public string? Template { get; set; }

    /// <summary>Gets or sets <c>template_data</c>.</summary>
    [JsonPropertyName("template_data")]
    public IReadOnlyDictionary<string, JsonElement>? TemplateData { get; set; }

    /// <summary>Gets or sets <c>template_id</c>.</summary>
    [JsonPropertyName("template_id")]
    public int? TemplateId { get; set; }

    /// <summary>Gets or sets <c>to</c>.</summary>
    [JsonPropertyName("to")]
    public required IReadOnlyList<string> To { get; set; }

    /// <summary>Gets or sets <c>unsubscribe</c>.</summary>
    [JsonPropertyName("unsubscribe")]
    public Shared.SendEmailUsingTemplateRequestUnsubscribe? Unsubscribe { get; set; }

    /// <summary>Gets or sets the <c>dry_run</c> query parameter.</summary>
    [JsonIgnore]
    public bool? DryRun { get; set; }

}