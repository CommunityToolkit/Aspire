var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.UseOutputCache();

app.MapDefaultEndpoints();

string[] summaries =
[
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
];

var helloWorldFn = static () => "Hello World!";
var weathersFn = () => 
    Enumerable.Range(0, 6).Select(index => 
        new WeatherForecast(
            DateTime.Now.AddDays(index),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        )
    );

app.MapGet("/hello", helloWorldFn);
app.MapGet("/api/weather", weathersFn);

var cachedGroup = app
    .MapGroup("")
    .CacheOutput(o => o.Cache());

cachedGroup.MapGet("/hello/cached", () => helloWorldFn);
cachedGroup.MapGet("/api/weather/cached", weathersFn);

app.Run();

record WeatherForecast(DateTime Date, int TemperatureC, string Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
