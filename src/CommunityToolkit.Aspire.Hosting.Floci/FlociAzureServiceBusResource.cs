using System.Globalization;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents the Service Bus AMQP data plane exposed by a Floci Azure emulator resource.
/// </summary>
/// <remarks>
/// floci-az serves Service Bus AMQP from an Artemis sidecar container that publishes the
/// configured ports directly on the Docker host — outside Aspire's endpoint model — so the
/// endpoint is host-relative (<c>localhost</c>). Sibling containers that need to consume
/// Service Bus must reach the sidecar over the Docker network instead.
/// </remarks>
/// <param name="name">The name of the resource.</param>
/// <param name="amqpPort">Host port the Artemis sidecar publishes for plain AMQP.</param>
/// <param name="amqpTlsPort">Host port the Artemis sidecar publishes for AMQPS (TLS).</param>
/// <param name="parent">The parent Floci Azure emulator resource.</param>
[AspireExport(ExposeProperties = true)]
public class FlociAzureServiceBusResource(
    string name,
    int amqpPort,
    int amqpTlsPort,
    FlociAzureContainerResource parent) : Resource(name),
    IResourceWithParent<FlociAzureContainerResource>,
    IResourceWithConnectionString
{
    internal const string DefaultName = "servicebus";

    // Placeholder from the official Service Bus emulator's connection-string shape; floci-az
    // does not enforce authentication, the SDK only requires the component to be present.
    internal const string DefaultSasKey = "SAS_KEY_VALUE";

    /// <summary>
    /// Gets the parent Floci Azure emulator resource.
    /// </summary>
    public FlociAzureContainerResource Parent { get; } = parent ?? throw new ArgumentNullException(nameof(parent));

    /// <summary>
    /// Gets the host port the Artemis sidecar publishes for plain AMQP.
    /// </summary>
    public int AmqpPort { get; } = amqpPort;

    /// <summary>
    /// Gets the host port the Artemis sidecar publishes for AMQPS (TLS).
    /// </summary>
    public int AmqpTlsPort { get; } = amqpTlsPort;

    /// <summary>
    /// Gets the Service Bus AMQP endpoint.
    /// </summary>
    public ReferenceExpression Endpoint
    {
        get
        {
            string port = AmqpPort.ToString(CultureInfo.InvariantCulture);
            return ReferenceExpression.Create($"sb://localhost:{port}");
        }
    }

    /// <summary>
    /// Gets the Service Bus connection string expression.
    /// <c>UseDevelopmentEmulator=true</c> makes the Azure SDKs use plain AMQP (no TLS), matching
    /// the official Service Bus emulator's connection-string shape.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"Endpoint={Endpoint};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey={DefaultSasKey};UseDevelopmentEmulator=true;");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties() =>
        Parent.CombineProperties([
            new("Endpoint", Endpoint)
        ]);
}
