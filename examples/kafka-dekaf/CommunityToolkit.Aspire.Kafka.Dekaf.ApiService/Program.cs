using Dekaf.Producer;
using Dekaf.SchemaRegistry;
using OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

builder.AddDekafSchemaRegistryClient("schema-registry");
builder.AddDekafKafkaAdminClient("messaging");
builder.AddDekafKafkaProducer<string, Message>("messaging", (services, producer) =>
    producer.UseJsonSchemaRegistry(services.GetRequiredService<ISchemaRegistryClient>(),
        """{"type":"object","properties":{"Text":{"type":"string"}},"required":["Text"]}"""));
builder.Services.AddOpenTelemetry().UseOtlpExporter();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapPost("/messages", async (Message message, IKafkaProducer<string, Message> producer, CancellationToken cancellationToken) =>
{
    var result = await producer.ProduceAsync("messages", "message", message, cancellationToken);
    return Results.Ok(new { result.Partition, result.Offset });
});

app.Run();

/// <summary>A message published by the example API.</summary>
/// <param name="Text">The message text.</param>
public sealed record Message(string Text);
