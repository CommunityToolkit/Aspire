using CommunityToolkit.Aspire.Java.WebApp.Models;

namespace CommunityToolkit.Aspire.Java.WebApp.Clients;

public interface IApiClient
{
    string Name { get; }

    Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync();
}
