// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Dapr.Client;
using Test;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddDaprClient();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// Configure the HTTP request pipeline.

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/secrets", (DaprClient client) =>
{
    var secrets = client.GetBulkSecretAsync("secretstore");
    return secrets;
});

app.MapGet("/weatherforecast", async (DaprClient client) =>
{
    await client.PublishEventAsync("pubsub", "weather", new WeatherForecastMessage("Weather forecast requested!"));

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

internal sealed record WeatherForecastMessage(string Message);

internal sealed record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

namespace Test
{
    public sealed class Worker(ILogger<Worker> logger, DaprClient dapr) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await dapr.WaitForSidecarAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var eventData = new WeatherEventData
                {
                    EventId = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.UtcNow,
                    Temperature = Random.Shared.Next(-20, 55),
                    Description = "Periodic weather update from Worker"
                };

                await dapr.PublishEventAsync(
                    "pubsub", 
                    "weather-periodic", 
                    eventData, 
                    cancellationToken: stoppingToken);

                logger.LogInformation("Published weather event: {EventId} at {Timestamp}", 
                    eventData.EventId, eventData.Timestamp);

                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    public sealed record WeatherEventData
    {
        public required string EventId { get; init; }
        public required DateTime Timestamp { get; init; }
        public required int Temperature { get; init; }
        public required string Description { get; init; }
    }
}
