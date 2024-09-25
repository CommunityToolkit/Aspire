using Aspire.CommunityToolkit.Hosting.Java.WebApp.Models;

namespace Aspire.CommunityToolkit.Hosting.Java.WebApp.Clients;

public interface IApiClient
{
    string Name { get; }

    Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync();
}
