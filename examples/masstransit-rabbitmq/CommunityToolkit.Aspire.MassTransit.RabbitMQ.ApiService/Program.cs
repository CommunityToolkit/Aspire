using CommunityToolkit.Aspire.MassTransit.RabbitMQ.ApiService;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMassTransitRabbitMq(
    "rmq",
    options => { options.DisableTelemetry = false; },
    masstransitConfiguration =>
    {
        masstransitConfiguration.AddConsumer<SubmitOrderConsumer>();
        masstransitConfiguration.AddConsumer<CancelOrderConsumer>();
        masstransitConfiguration.AddConsumer<UpdateOrderConsumer>();
    }
);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();