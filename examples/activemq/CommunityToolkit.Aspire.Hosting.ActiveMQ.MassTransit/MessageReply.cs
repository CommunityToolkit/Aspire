namespace CommunityToolkit.Aspire.Hosting.ActiveMQ.MassTransit;

public record MessageReply
{
    public required string Reply { get; set; }
}