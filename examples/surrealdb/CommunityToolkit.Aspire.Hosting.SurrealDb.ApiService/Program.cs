using CommunityToolkit.Aspire.Hosting.SurrealDb.ApiService.Models;
using SurrealDb.Net;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.AddServiceDefaults();

builder.AddSurrealClient("db");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGroup("/api")
    .MapSurrealEndpoints<WeatherForecast, SurrealDbSession>(
        "/weatherForecast",
        new() { EnableMutations = false }
    )
    .MapSurrealEndpoints<Todo, SurrealDbSession>("/todo");

app.MapPost("/init", InitializeDbAsync);

app.Run();

async Task InitializeDbAsync()
{
    const int initialCount = 5;
    var weatherForecasts = new WeatherForecastFaker().Generate(initialCount);
    var todos = new TodoFaker().Generate(initialCount);
    
    var surrealDbClient = new SurrealDbClient(
        SurrealDbOptions
            .Create()
            .FromConnectionString(configuration.GetConnectionString("db")!)
            .Build()
    );

    var weatherForecastTasks = weatherForecasts.Select(async weatherForecast => {
        await surrealDbClient.Create(WeatherForecast.Table, weatherForecast);
        return Task.CompletedTask;
    });
    
    var todoTasks = todos.Select(async todo => {
        await surrealDbClient.Create(Todo.Table, todo);
        return Task.CompletedTask;
    });

    await Task.WhenAll(weatherForecastTasks.Concat(todoTasks));

    await surrealDbClient.DisposeAsync();
}