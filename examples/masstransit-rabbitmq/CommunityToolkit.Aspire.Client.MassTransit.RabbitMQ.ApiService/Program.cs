using CommunityToolkit.Aspire.Client.MassTransit.RabbitMQ.ApiService;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddMassTransitRabbitMq(
    "rmq",
    options => { options.DisableTelemetry = false; },
    consumers =>
    {
        consumers.AddConsumer<SubmitOrderConsumer>();
        consumers.AddConsumer<CancelOrderConsumer>();
        consumers.AddConsumer<UpdateOrderConsumer>();
    }
);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();