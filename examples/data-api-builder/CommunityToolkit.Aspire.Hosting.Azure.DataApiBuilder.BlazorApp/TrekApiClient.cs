using System.Text.Json.Serialization;

namespace CommunityToolkit.Aspire.Hosting.Azure.DataApiBuilder.BlazorApp;

public class TrekApiClient(HttpClient httpClient, ILogger<TrekApiClient> logger)
{
    public async Task<List<Series>> GetSeriesAsync()
    {
        var response = await httpClient.GetAsync($"api/series");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DabResponse<Series>>();

        if(result is null)
        {
            logger.LogError("Failed to deserialize response from Data API Builder.");
            throw new Exception("Failed to deserialize response from Data API Builder.");
        }

        if (result.Error is not null)
        {
            logger.LogError("API error: {Code} - {Message}", result.Error.Code, result.Error.Message);
            throw new Exception($"{result.Error.Code}: {result.Error.Message}");
        }

        return result.Value ?? [];
    }
}

public class DabResponse<T>
{
    [JsonPropertyName("value")]
    public List<T>? Value { get; set; }

    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }

    [JsonPropertyName("error")]
    public DabError? Error { get; set; }
}

public class DabError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }
}

public class Series
{
    public required string Name { get; set; }
}
