namespace CommunityToolkit.Aspire.Hosting.MassTransit.RabbitMQ;

/// <summary>
/// Configuration options for MassTransit integration (same as host-side).
/// </summary>
public sealed class MassTransitRabbitMqOptions
{
    /// <summary>
    /// Username to use for RabbitMQ.
    /// </summary>
    public string Username { get; set; } = string.Empty;
    /// <summary>
    /// Password to use for RabbitMQ.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Port to use for RabbitMQ.
    /// </summary>
    public int? Port { get; set; }
}
