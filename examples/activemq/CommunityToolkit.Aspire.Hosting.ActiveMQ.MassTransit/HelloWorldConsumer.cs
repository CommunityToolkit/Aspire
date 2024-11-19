using MassTransit;

namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.MassTransit;

public class HelloWorldConsumer : IConsumer<Message>
{
    private readonly ILogger<HelloWorldConsumer> _logger;

    public HelloWorldConsumer(ILogger<HelloWorldConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<Message> context)
    {
        _logger.LogInformation("Received message: {Text}", context.Message.Text);
        return Task.CompletedTask;
    }
}