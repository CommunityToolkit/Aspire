
using CommunityToolkit.Aspire.Client.MassTransit.RabbitMQ.ApiService;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMassTransitRabbitMq(
    "rmq",
    options => { options.DisableTelemetry = false; },
    x =>
    {
        x.AddConsumer<SubmitOrderConsumer>();
        x.AddConsumer<CancelOrderConsumer>();
        x.AddConsumer<UpdateOrderConsumer>();
    });

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();

