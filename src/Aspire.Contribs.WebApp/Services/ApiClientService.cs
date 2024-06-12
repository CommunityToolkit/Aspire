using Aspire.Contribs.WebApp.Clients;
using Aspire.Contribs.WebApp.Models;

namespace Aspire.Contribs.WebApp.Services;

public interface IApiClientService
{
    Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync(string clientName);
}

public class ApiClientService(IEnumerable<IApiClient> clients) : IApiClientService
{
    private readonly IEnumerable<IApiClient> _clients = clients ?? throw new ArgumentNullException(nameof(clients));

    public async Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync(string clientName)
    {
        var client = this._clients.SingleOrDefault(p => p.Name.Equals(clientName, StringComparison.InvariantCultureIgnoreCase));
        if (client is null)
        {
            throw new ArgumentException("No API client found.");
        }

        var forecasts = await client.GetWeatherForecastAsync().ConfigureAwait(false);

        return forecasts ?? [];
    }
}