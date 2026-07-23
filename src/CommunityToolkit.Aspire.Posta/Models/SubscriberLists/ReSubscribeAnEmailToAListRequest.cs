using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.SubscriberLists;

/// <summary>Represents the ReSubscribeAnEmailToAListRequest payload.</summary>
public class ReSubscribeAnEmailToAListRequest
{
    /// <summary>Gets or sets <c>email</c>.</summary>
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    /// <summary>Gets or sets the <c>id</c> path parameter.</summary>
    [JsonIgnore]
    public required int Id { get; set; }

}