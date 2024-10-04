using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.Json;


namespace Aspire.CommunityToolkit.Hosting.DataApiBuilder.BlazorApp;

public class TrekApiClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<TrekApiClient> logger;

    public TrekApiClient(HttpClient httpClient, ILogger<TrekApiClient> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<List<Series>> GetSeriesAsync()
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<SeriesList>($"api/series");
            return result?.value ?? new List<Series>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while fetching series.");
            return new List<Series>();
        }
    }
}

public class SeriesList
{
    public List<Series> value { get; set; } = new List<Series>();
}

public class Series
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Id { get; set; }
    public required string Name { get; set; }

}
