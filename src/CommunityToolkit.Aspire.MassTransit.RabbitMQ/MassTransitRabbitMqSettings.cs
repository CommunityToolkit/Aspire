namespace CommunityToolkit.Aspire.MassTransit.RabbitMQ;

/// <summary>
/// Configuration options for the MassTransit RabbitMQ client integration.
/// </summary>
public sealed class MassTransitRabbitMqSettings
{
    /// <summary>
    /// Disables telemetry if set to true.
    /// </summary>
    public bool DisableTelemetry { get; set; }
}