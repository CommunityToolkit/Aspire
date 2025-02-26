namespace CommunityToolkit.Aspire.Hosting.LavinMQ.MassTransit;

public record Message
{
    public required string Text { get; set; }
}