// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dapr;
using Dapr.Client;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddDaprClient();
builder.Services.AddHttpClient("serviceb", client =>
    client.BaseAddress = new Uri("http://serviceb"))
    .AddHttpMessageHandler(() => new InvocationHandler());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCloudEvents();
app.MapSubscribeHandler();

app.MapGet("/weatherforecast", async (DaprClient client, IHttpClientFactory httpClientFactory) =>
{
    var cachedForecasts = await client.GetStateAsync<CachedWeatherForecast>("statestore", "cache");

    if (cachedForecasts is not null && cachedForecasts.CachedAt > DateTimeOffset.UtcNow.AddMinutes(-1))
    {
        return cachedForecasts.Forecasts;
    }

    var httpClient = httpClientFactory.CreateClient("serviceb");
    var forecasts = await httpClient.GetFromJsonAsync<WeatherForecast[]>("/weatherforecast")
        ?? throw new InvalidOperationException("Failed to retrieve weather forecasts from serviceb.");

    await client.SaveStateAsync("statestore", "cache", new CachedWeatherForecast(forecasts, DateTimeOffset.UtcNow));

    return forecasts;
})
.WithName("GetWeatherForecast");

app.MapPost("/subscriptions/weather", [Topic("pubsub", "weather")] (ILogger<Program> logger, WeatherForecastMessage message) =>
{
    logger.LogInformation("Weather forecast message received: {Message}", message.Message);
});

app.MapDefaultEndpoints();

app.Run();

internal sealed record WeatherForecastMessage(string Message);

internal sealed record CachedWeatherForecast(WeatherForecast[] Forecasts, DateTimeOffset CachedAt);

internal sealed record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
