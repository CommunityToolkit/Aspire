using MassTransit;
using Messaging;

namespace CommunityToolkit.Aspire.Client.MassTransit.RabbitMQ.ApiService;

using System;
using System.Threading.Tasks;


public class SubmitOrderConsumer : IConsumer<MessageTypes.SubmitOrder>
{
    public Task Consume(ConsumeContext<MessageTypes.SubmitOrder> context)
    {
        Console.WriteLine($"SubmitOrderConsumer received: {context.Message.OrderId}");
        return Task.CompletedTask;
    }
}

public class CancelOrderConsumer : IConsumer<MessageTypes.CancelOrder>
{
    public Task Consume(ConsumeContext<MessageTypes.CancelOrder> context)
    {
        Console.WriteLine($"CancelOrderConsumer received: {context.Message.OrderId}");
        return Task.CompletedTask;
    }
}


public class UpdateOrderConsumer : IConsumer<MessageTypes.UpdateOrder>
{
    public Task Consume(ConsumeContext<MessageTypes.UpdateOrder> context)
    {
        Console.WriteLine($"UpdateOrderConsumer received: {context.Message.OrderId}");
        return Task.CompletedTask;
    }
}
