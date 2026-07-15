using SurrealDb.Net.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommunityToolkit.Aspire.Hosting.SurrealDb.ApiService.Models;

/// <summary>
/// Weather forecast model.
/// </summary>
[Table(Table)]
public class WeatherForecast : Record
{
    internal const string Table = "weatherForecast";

    /// <summary>
    /// Date of the weather forecast.
    /// </summary>
    [Column("date")]
    public DateTime Date { get; set; }

    /// <summary>
    /// Country of the weather forecast.
    /// </summary>
    [Column("country")]
    public string? Country { get; set; }

    /// <summary>
    /// Temperature in Celsius.
    /// </summary>
    [Column("temperature_c")]
    public int TemperatureC { get; set; }

    /// <summary>
    /// Temperature in Fahrenheit.
    /// </summary>
    [Column("temperature_f")]
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    /// <summary>
    /// Summary of the weather forecast.
    /// </summary>
    [Column("summary")]
    public string? Summary { get; set; }
}