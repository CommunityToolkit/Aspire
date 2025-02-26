using MassTransit;

namespace CommunityToolkit.Aspire.Hosting.LavinMQ.MassTransit;

public class HelloWorldConsumer(ILogger<HelloWorldConsumer> logger, MessageCounter messageCounter) : IConsumer<Message>
{
    public async Task Consume(ConsumeContext<Message> context)
    {
        logger.LogInformation("Received message: {Text}", context.Message.Text);
        messageCounter.ReceivedMessages++;
        await context.RespondAsync<MessageReply>(new
        {
            Reply = "I've received your message: " + context.Message.Text
        });
    }
}