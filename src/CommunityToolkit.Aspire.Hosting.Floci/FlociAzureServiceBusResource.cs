namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents the Service Bus AMQP data plane exposed by a Floci Azure emulator resource.
/// </summary>
/// <remarks>
/// floci-az serves Service Bus AMQP from an Artemis sidecar container that publishes the
/// configured ports directly on the Docker host. The resource models those host ports as
/// proxyless Aspire endpoints so DCP can allocate them without trying to proxy traffic to the
/// parent container.
/// </remarks>
/// <param name="name">The name of the resource.</param>
/// <param name="parent">The parent Floci Azure emulator resource.</param>
[AspireExport(ExposeProperties = true)]
public class FlociAzureServiceBusResource(
    string name,
    FlociAzureContainerResource parent) : Resource(name),
    IResourceWithParent<FlociAzureContainerResource>,
    IResourceWithConnectionString,
    IResourceWithEndpoints
{
    internal const string DefaultName = "servicebus";
    internal const string AmqpEndpointName = "amqp";
    internal const string AmqpTlsEndpointName = "amqps";

    private EndpointReference? _amqpEndpoint;
    private EndpointReference? _amqpTlsEndpoint;

    // Placeholder from the official Service Bus emulator's connection-string shape; floci-az
    // does not enforce authentication, the SDK only requires the component to be present.
    internal const string DefaultSasKey = "SAS_KEY_VALUE";

    /// <summary>
    /// Gets the parent Floci Azure emulator resource.
    /// </summary>
    public FlociAzureContainerResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    /// <summary>
    /// Gets the Service Bus plain AMQP endpoint.
    /// </summary>
    public EndpointReference AmqpEndpoint =>
        _amqpEndpoint ??= new EndpointReference(this, AmqpEndpointName);

    /// <summary>
    /// Gets the Service Bus AMQPS/TLS endpoint.
    /// </summary>
    public EndpointReference AmqpTlsEndpoint =>
        _amqpTlsEndpoint ??= new EndpointReference(this, AmqpTlsEndpointName);

    /// <summary>
    /// Gets the Service Bus connection string expression.
    /// <c>UseDevelopmentEmulator=true</c> makes the Azure SDKs use plain AMQP (no TLS), matching
    /// the official Service Bus emulator's connection-string shape.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"Endpoint={AmqpEndpoint};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey={DefaultSasKey};UseDevelopmentEmulator=true;");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties() =>
        Parent.CombineProperties([
            new("Host", ReferenceExpression.Create($"{AmqpEndpoint.Property(EndpointProperty.Host)}")),
            new("Port", ReferenceExpression.Create($"{AmqpEndpoint.Property(EndpointProperty.Port)}")),
            new("Uri", ReferenceExpression.Create($"{AmqpEndpoint}")),
            new("Endpoint", ReferenceExpression.Create($"{AmqpEndpoint}"))
        ]);
}
