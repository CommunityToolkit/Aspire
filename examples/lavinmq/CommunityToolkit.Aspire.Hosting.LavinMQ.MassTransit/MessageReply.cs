namespace CommunityToolkit.Aspire.Hosting.LavinMQ.MassTransit;

public record MessageReply
{
    public required string Reply { get; set; }
}