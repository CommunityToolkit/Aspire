using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Client.MassTransit.RabbitMQ;

/// <summary>
/// Configuration options for the MassTransit RabbitMQ client integration.
/// </summary>
public sealed class MassTransitRabbitMqOptions
{
    /// <summary>
    /// Username to use for RabbitMQ.
    /// </summary>
    public IResourceBuilder<ParameterResource>? UsernameKey { get; set; }

    /// <summary>
    /// Password to use for RabbitMQ.
    /// </summary>
    public IResourceBuilder<ParameterResource>? PasswordKey { get; set; }

    /// <summary>
    /// Disables telemetry if set to true.
    /// </summary>
    public bool DisableTelemetry { get; set; }
    
    /// <summary>
    /// Virtual host to use for RabbitMQ.
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Host to use for RabbitMQ.
    /// </summary>
    public string Host { get; set; } = "localhost";
}