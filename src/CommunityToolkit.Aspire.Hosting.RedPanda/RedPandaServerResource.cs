namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Redpanda streaming platform container.
/// </summary>
/// <remarks>
/// Redpanda is a Kafka API compatible streaming platform. The <see cref="ConnectionStringExpression"/>
/// returns the bootstrap broker address (<c>host:port</c>) that Kafka clients use to connect.
/// </remarks>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class RedPandaServerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string PrimaryEndpointName = "kafka";
    internal const string InternalEndpointName = "internal";
    internal const string SchemaRegistryEndpointName = "schemaregistry";
    internal const string AdminEndpointName = "admin";

    internal const int KafkaBrokerPort = 9092;
    internal const int KafkaInternalBrokerPort = 29092;
    internal const int SchemaRegistryPort = 8081;
    internal const int AdminPort = 9644;

    private EndpointReference? _primaryEndpoint;
    private EndpointReference? _internalEndpoint;
    private EndpointReference? _schemaRegistryEndpoint;
    private EndpointReference? _adminEndpoint;

    /// <summary>
    /// Gets the primary (Kafka API) endpoint for the Redpanda broker. This endpoint is reachable from the host.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);

    /// <summary>
    /// Gets the internal Kafka API endpoint used for container-to-container communication.
    /// </summary>
    public EndpointReference InternalEndpoint => _internalEndpoint ??= new(this, InternalEndpointName);

    /// <summary>
    /// Gets the Schema Registry HTTP endpoint for the Redpanda broker.
    /// </summary>
    public EndpointReference SchemaRegistryEndpoint => _schemaRegistryEndpoint ??= new(this, SchemaRegistryEndpointName);

    /// <summary>
    /// Gets the Admin API HTTP endpoint for the Redpanda broker.
    /// </summary>
    public EndpointReference AdminEndpoint => _adminEndpoint ??= new(this, AdminEndpointName);

    /// <summary>
    /// Gets the host of the primary Kafka endpoint.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port of the primary Kafka endpoint.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the connection string expression for the Redpanda broker in the form of <c>host:port</c>,
    /// suitable for use as the Kafka bootstrap servers value.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create($"{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
    }
}
