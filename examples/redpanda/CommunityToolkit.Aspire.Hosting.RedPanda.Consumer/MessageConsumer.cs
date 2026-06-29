using Confluent.Kafka;

namespace CommunityToolkit.Aspire.Hosting.RedPanda.Consumer;

/// <summary>
/// A background service that consumes messages from the Redpanda broker and logs them, demonstrating
/// how a referencing application reads from the resource added by the hosting integration.
/// </summary>
internal sealed class MessageConsumer(
    IConsumer<string, string> consumer,
    ILogger<MessageConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        // The Confluent consumer loop is synchronous and blocking, so run it on a background
        // thread to avoid blocking host startup.
        => Task.Run(() => Consume(stoppingToken), stoppingToken);

    private void Consume(CancellationToken stoppingToken)
    {
        consumer.Subscribe(KafkaTopics.Messages);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string> result = consumer.Consume(stoppingToken);

                logger.LogInformation(
                    "Consumed message {Key} from {TopicPartitionOffset}: {Value}",
                    result.Message.Key,
                    result.TopicPartitionOffset,
                    result.Message.Value);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested; nothing to do.
        }
        finally
        {
            consumer.Close();
        }
    }
}

internal static class KafkaTopics
{
    public const string Messages = "messages";
}
