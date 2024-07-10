using CommunityToolkit.Aspire.WebApp.Models;

namespace CommunityToolkit.Aspire.WebApp.Clients;

public interface IApiClient
{
    string Name { get; }

    Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync();
}
