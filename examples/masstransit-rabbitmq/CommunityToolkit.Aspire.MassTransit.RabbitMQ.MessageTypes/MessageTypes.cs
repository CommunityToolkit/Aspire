namespace Messaging;

public class MessageTypes
{
    // Message Contracts
    public record SubmitOrder(Guid OrderId);
    public record CancelOrder(Guid OrderId);
    public record UpdateOrder(Guid OrderId);
}