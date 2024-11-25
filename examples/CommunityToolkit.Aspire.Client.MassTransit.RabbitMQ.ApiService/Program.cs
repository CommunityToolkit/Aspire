
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMassTransitRabbitMq("rmq",options =>
{
    options.DisableTelemetry = false;
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();