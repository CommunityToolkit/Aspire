using Confluent.Kafka;
using CommunityToolkit.Aspire.Hosting.RedPanda.Consumer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Redpanda is Kafka API compatible, so the standard Aspire.Confluent.Kafka client integration
// connects to it using the connection string published by the "redpanda" resource.
builder.AddKafkaProducer<string, string>("redpanda");
builder.AddKafkaConsumer<string, string>("redpanda", settings =>
{
    settings.Config.GroupId = "redpanda-consumer";
    settings.Config.AutoOffsetReset = AutoOffsetReset.Earliest;
});

builder.Services.AddHostedService<MessageConsumer>();

var app = builder.Build();

// Publish a message to the topic that the background consumer is reading from.
app.MapPost("/messages", async (MessagePayload payload, IProducer<string, string> producer, CancellationToken cancellationToken) =>
{
    var result = await producer.ProduceAsync(
        KafkaTopics.Messages,
        new Message<string, string> { Key = Guid.NewGuid().ToString(), Value = payload.Text },
        cancellationToken);

    return Results.Ok(new { result.Topic, Partition = result.Partition.Value, Offset = result.Offset.Value });
});

app.MapDefaultEndpoints();

app.Run();

internal sealed record MessagePayload(string Text);
