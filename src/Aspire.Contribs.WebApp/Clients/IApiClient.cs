using Aspire.Contribs.WebApp.Models;

namespace Aspire.Contribs.WebApp.Clients;

public interface IApiClient
{
    string Name { get; }

    Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync();
}
