namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Configuration options for the MassTransit RabbitMQ client integration.
/// </summary>
public sealed class MassTransitRabbitMqOptions
{
    /// <summary>
    /// Disables Telemetry.
    /// </summary>
    public bool DisableTelemetry { get; set; }
}