using Microsoft.Extensions.Hosting;
var builder = WebApplication.CreateBuilder(args);


builder.AddServiceDefaults();

builder.Services.AddMassTransitRabbitMqClient("RabbitMQInstance", telemetry: true);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();