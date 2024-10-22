using CommunityToolkit.Aspire.Hosting.Java.WebApp.Models;

namespace CommunityToolkit.Aspire.Hosting.Java.WebApp.Clients;

public class WebApiClient(HttpClient http) : IApiClient
{
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public string Name => "WebApi";

    public async Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync()
    {
        var forecasts = await _http.GetFromJsonAsync<IEnumerable<WeatherForecast>>("/weatherforecast").ConfigureAwait(false);

        return forecasts ?? [];
    }
}
