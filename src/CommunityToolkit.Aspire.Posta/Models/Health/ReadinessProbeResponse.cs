using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Posta.Models.Health;

/// <summary>Represents the ReadinessProbeResponse payload.</summary>
public class ReadinessProbeResponse
{
    /// <summary>Gets or sets <c>database</c>.</summary>
    [JsonPropertyName("database")]
    public string? Database { get; set; }

    /// <summary>Gets or sets <c>redis</c>.</summary>
    [JsonPropertyName("redis")]
    public string? Redis { get; set; }

    /// <summary>Gets or sets <c>status</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

}