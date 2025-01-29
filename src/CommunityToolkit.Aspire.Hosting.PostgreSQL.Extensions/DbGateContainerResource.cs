using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.DbGate;

/// <summary>
/// Represents a container resource for DbGate.
/// </summary>
/// <param name="name">The name of the container resource.</param>
public sealed class DbGateContainerResource(string name) : ContainerResource(name)
{
    internal const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary endpoint for the DbGate.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
}