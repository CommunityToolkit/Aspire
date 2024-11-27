namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.MassTransit;

public record Message
{
    public required string Text { get; set; }
}