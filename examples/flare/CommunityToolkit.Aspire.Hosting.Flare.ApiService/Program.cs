using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Plain OpenTelemetry SDK setup, nothing Flare-specific: the AppHost's WithOtlpExporter(flare) sets
// OTEL_EXPORTER_OTLP_ENDPOINT and OTEL_EXPORTER_OTLP_PROTOCOL, which UseOtlpExporter reads.
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(Orders.TelemetryName)
        .AddAspNetCoreInstrumentation(options => options.RecordException = true)
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter(Orders.TelemetryName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation())
    .UseOtlpExporter();

builder.Services.AddSingleton<Orders>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<TrafficGenerator>();

var app = builder.Build();

app.MapPost("/orders", (Orders orders) => orders.Place());

app.Run();

// Places an order. Every call emits a span, a log line and a metric; roughly one in five fails with
// an exception, so Flare's errors view has something to show.
sealed class Orders(ILogger<Orders> logger)
{
    public const string TelemetryName = "Flare.Example.Orders";

    private static readonly ActivitySource ActivitySource = new(TelemetryName);
    private static readonly Meter Meter = new(TelemetryName);
    private static readonly Counter<long> Placed = Meter.CreateCounter<long>("orders.placed");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("orders.failed");
    private static readonly Histogram<double> Value = Meter.CreateHistogram<double>("orders.value", unit: "USD");

    private int _nextId;

    public object Place()
    {
        var orderId = Interlocked.Increment(ref _nextId);
        var items = Random.Shared.Next(1, 6);
        var total = Math.Round(items * Random.Shared.NextDouble() * 40, 2);

        using var activity = ActivitySource.StartActivity("PlaceOrder");
        activity?.SetTag("order.id", orderId);
        activity?.SetTag("order.items", items);

        if (Random.Shared.Next(5) == 0)
        {
            Failed.Add(1);
            var exception = new InvalidOperationException($"Payment declined for order {orderId}.");
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            logger.LogError(exception, "Order {OrderId} failed", orderId);
            throw exception;
        }

        Placed.Add(1);
        Value.Record(total);
        logger.LogInformation("Placed order {OrderId} with {ItemCount} items for {Total} USD", orderId, items, total);

        return new { orderId, items, total };
    }
}

// Calls the service's own endpoint every couple of seconds, so telemetry flows without anyone
// having to send requests by hand.
sealed class TrafficGenerator(IServer server, IHttpClientFactory httpClientFactory, IHostApplicationLifetime lifetime, ILogger<TrafficGenerator> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The server's address is only known once it has started listening.
        var started = new TaskCompletionSource();
        using (lifetime.ApplicationStarted.Register(() => started.SetResult()))
        {
            await started.Task.WaitAsync(stoppingToken);
        }

        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(address);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var response = await client.PostAsync("/orders", content: null, stoppingToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Could not reach {Address}", address);
            }
        }
    }
}
