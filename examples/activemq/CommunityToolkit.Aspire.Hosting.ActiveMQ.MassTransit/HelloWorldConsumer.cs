using MassTransit;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.MassTransit;

public class HelloWorldConsumer(ILogger<HelloWorldConsumer> logger) : IConsumer<Message>
{
    public Task Consume(ConsumeContext<Message> context)
    {
        logger.LogInformation("Received message: {Text}", context.Message.Text);
        return Task.CompletedTask;
    }
}