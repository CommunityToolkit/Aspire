using MassTransit;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.MassTransit;

public class HelloWorldConsumer(ILogger<HelloWorldConsumer> logger, MessageCounter messageCounter) : IConsumer<Message>
{
    public Task Consume(ConsumeContext<Message> context)
    {
        logger.LogInformation("Received message: {Text}", context.Message.Text);
        messageCounter.ReceivedMessages++;
        return Task.CompletedTask;
    }
}