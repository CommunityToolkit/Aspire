using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.MassTransit.RabbitMQ;

/// <summary>
/// Configuration options for MassTransit integration (same as host-side).
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
    /// Port to use for RabbitMQ.
    /// </summary>
    public int? Port { get; set; }
}
