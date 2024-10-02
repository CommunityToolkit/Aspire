using System.Net.Http;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Aspire.CommunityToolkit.Hosting.DAB.BlazorApp;

public class TrekApiClientClass(HttpClient httpClient)
{
    public async Task<List<Series>> GetSeriesAsync()
    {
        var result = await httpClient.GetFromJsonAsync<SeriesList>($"api/series");
        return result?.value ?? new List<Series>();
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
