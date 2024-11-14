using CommunityToolkit.Aspire.Hosting.SurrealDb.ApiService.Models;
using SurrealDb.Net;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSurrealClient("db", settings =>
{
    // TODO : Wait for v0.7
    // settings.Options!.NamingPolicy = "CamelCase";
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGroup("/api")
    .MapSurrealEndpoints<WeatherForecast>(
        "/weatherForecast",
        new() { EnableMutations = false }
    )
    .MapSurrealEndpoints<Todo>("/todo");

app.MapPost("/init", InitializeDbAsync);

app.Run();

Task InitializeDbAsync(ISurrealDbClient surrealDbClient)
{
    const int initialCount = 5;
    var weatherForecasts = new WeatherForecastFaker().Generate(initialCount);

    var tasks = weatherForecasts.Select(weatherForecast =>
        surrealDbClient.Create(WeatherForecast.Table, weatherForecast)
    );

    return Task.WhenAll(tasks);
}