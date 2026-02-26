namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents Data API Builder.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="entrypoint">An optional container entrypoint.</param>
public sealed class DataApiBuilderContainerResource(string name, string? entrypoint = null)
    : ContainerResource(name, entrypoint), IResourceWithServiceDiscovery
{
    internal const string HttpEndpointName = "http";

    internal const int HttpEndpointPort = 5000;

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary HTTP endpoint for the Data API Builder container.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, HttpEndpointName);
}
