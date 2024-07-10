using Aspire.Contribs.WebApp.Models;

namespace Aspire.Contribs.WebApp.Clients;

public class SpringContainerClient(HttpClient http) : IApiClient
{
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public string Name => "Spring Container";

    public async Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync()
    {
        var forecasts = await _http.GetFromJsonAsync<IEnumerable<WeatherForecast>>("/api/weatherforecast").ConfigureAwait(false);

        return forecasts ?? [];
    }
}

public class SpringExecutableClient(HttpClient http) : IApiClient
{
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public string Name => "Spring Executable";

    public async Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync()
    {
        var forecasts = await _http.GetFromJsonAsync<IEnumerable<WeatherForecast>>("/api/weatherforecast").ConfigureAwait(false);

        return forecasts ?? [];
    }
}
