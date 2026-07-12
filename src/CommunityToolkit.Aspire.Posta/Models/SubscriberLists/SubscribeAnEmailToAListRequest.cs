using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.SubscriberLists;

/// <summary>Represents the SubscribeAnEmailToAListRequest payload.</summary>
public class SubscribeAnEmailToAListRequest
{
    /// <summary>Gets or sets <c>email</c>.</summary>
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    /// <summary>Gets or sets <c>list</c>.</summary>
    [JsonPropertyName("list")]
    public required string List { get; set; }

    /// <summary>Gets or sets <c>name</c>.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

}