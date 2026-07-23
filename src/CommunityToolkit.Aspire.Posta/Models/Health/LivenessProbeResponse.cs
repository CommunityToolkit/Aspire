using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Health;

/// <summary>Represents the LivenessProbeResponse payload.</summary>
public class LivenessProbeResponse
{
    /// <summary>Gets or sets <c>status</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

}