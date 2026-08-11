#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Mosquitto MQTT broker container.
/// </summary>
/// <remarks>
/// Mosquitto is an open source MQTT message broker. The <see cref="ConnectionStringExpression"/>
/// returns a <c>mqtt://host:port</c> URI that MQTT clients use to connect.
/// </remarks>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class MosquittoServerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "tcp";
    internal const string PrimaryEndpointScheme = "tcp";
    internal const string MqttScheme = "mqtt";
    internal const int DefaultPort = 1883;

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary MQTT endpoint for the Mosquitto broker.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the host of the primary MQTT endpoint.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port of the primary MQTT endpoint.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the connection string expression for the Mosquitto broker in the form of <c>mqtt://host:port</c>,
    /// suitable for use by MQTT clients.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{MqttScheme}://{Host}:{Port}");

    /// <summary>
    /// Gets the connection URI expression for the Mosquitto broker.
    /// </summary>
    /// <remarks>
    /// Format: <c>mqtt://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ConnectionStringExpression;

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}

#pragma warning restore ASPIREATS001
