using System.Text.Json.Serialization;

internal class AdminerLoginServer
{
    [JsonPropertyName("server")]
    public string? Server { get; set; }

    [JsonPropertyName("username")]
    public string? UserName { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("driver")]
    public string? Driver { get; set; }
}