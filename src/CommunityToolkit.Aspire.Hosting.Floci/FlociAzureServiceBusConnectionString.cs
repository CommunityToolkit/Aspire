using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Floci;

// The sidecar publishes directly on the Docker host. Expose the parent as the dependency
// so Aspire does not create a container tunnel for the child's proxyless listeners.
internal sealed class FlociAzureServiceBusConnectionString(FlociAzureServiceBusResource resource)
    : IValueProvider, IManifestExpressionProvider, IValueWithReferences
{
    internal const string ContainerHost = "floci-servicebus-host.internal";

    public async ValueTask<string?> GetValueAsync(CancellationToken cancellationToken = default)
    {
        string? port = await resource.AmqpEndpoint.Property(EndpointProperty.Port).GetValueAsync(cancellationToken);
        return $"Endpoint=sb://{ContainerHost}:{port};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey={FlociAzureServiceBusResource.DefaultSasKey};UseDevelopmentEmulator=true;";
    }

    public string ValueExpression => throw new NotSupportedException("Floci Service Bus references are only supported in run mode.");

    public IEnumerable<object> References => [resource.Parent];
}
