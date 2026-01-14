namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a container resource for Elasticvue.
/// </summary>
/// <param name="name">The name of the container resource.</param>
public sealed class ElasticvueContainerResource(string name) : ContainerResource(name)
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the Elasticvue.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
}
