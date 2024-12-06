namespace CommunityToolkit.Aspire.MassTransit.RabbitMQ.Tests;

public class ConfigurationTests
{
    [Fact]
    public void DisableTelemetryIsFalseByDefault() =>
        Assert.False(new MassTransitRabbitMqSettings().DisableTelemetry);
}