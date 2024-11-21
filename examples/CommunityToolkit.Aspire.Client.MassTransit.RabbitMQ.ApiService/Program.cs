using CommunityToolkit.Aspire.Client.MassTransit.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);


builder.AddServiceDefaults();

builder.Services.AddMassTransitClient("RabbitMQInstance", telemetry: true);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();