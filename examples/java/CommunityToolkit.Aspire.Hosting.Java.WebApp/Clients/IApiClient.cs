using CommunityToolkit.Aspire.Hosting.Java.WebApp.Models;

namespace CommunityToolkit.Aspire.Hosting.Java.WebApp.Clients;

public interface IApiClient
{
    string Name { get; }

    Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync();
}
