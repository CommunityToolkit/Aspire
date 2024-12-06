using MassTransit;
using Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddMassTransitRabbitMq("rmq");

builder.Services.AddHostedService<MessageProducerService>();

var app = builder.Build();

app.MapGet("/", () => "Message Producer is running.");

await app.RunAsync();

public class MessageProducerService : IHostedService
{
    private readonly IBus _bus;
    private Timer? _timer;

    public MessageProducerService(IBus bus)
    {
        _bus = bus;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("MessageProducerService started...");
        _timer = new Timer(SendMessages, null, 0, (int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("MessageProducerService stopping...");
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _timer?.Dispose();
        return Task.CompletedTask;
    }

    private async void SendMessages(object? state)
    {
        try
        {
            var orderId = Guid.NewGuid();

            // Send SubmitOrder message
            await _bus.Publish(new MessageTypes.SubmitOrder(orderId));
            Console.WriteLine($"Sent SubmitOrder: {orderId}");

            // Send CancelOrder message
            await _bus.Publish(new MessageTypes.CancelOrder(orderId));
            Console.WriteLine($"Sent CancelOrder: {orderId}");

            // Send UpdateOrder message
            await _bus.Publish(new MessageTypes.UpdateOrder(orderId));
            Console.WriteLine($"Sent UpdateOrder: {orderId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while sending messages: {ex.Message}");
        }
    }
}
