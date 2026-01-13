using System.Text.Json.Serialization;

namespace Aspire.Hosting;

internal record ElasticvueEnvironmentSettings(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);