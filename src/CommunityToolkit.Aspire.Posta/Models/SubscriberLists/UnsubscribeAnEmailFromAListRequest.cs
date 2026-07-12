using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.SubscriberLists;

/// <summary>Represents the UnsubscribeAnEmailFromAListRequest payload.</summary>
public class UnsubscribeAnEmailFromAListRequest
{
    /// <summary>Gets or sets <c>email</c>.</summary>
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    /// <summary>Gets or sets <c>reason</c>.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the <c>id</c> path parameter.</summary>
    [JsonIgnore]
    public required int Id { get; set; }

}