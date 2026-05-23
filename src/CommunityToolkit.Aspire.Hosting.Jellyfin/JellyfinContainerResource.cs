#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the Jellyfin media server.
/// </summary>
/// <param name="name">The name of the resource.</param>
[AspireExport(ExposeProperties = true)]
public class JellyfinContainerResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const int HttpEndpointPort = 8096;
    internal const int DiscoveryEndpointPort = 7359;
    internal const int DlnaEndpointPort = 1900;

    internal const string HttpEndpointName = "http";
    internal const string DiscoveryEndpointName = "discovery";
    internal const string DlnaEndpointName = "dlna";

    internal const string ConfigTarget = "/config";
    internal const string CacheTarget = "/cache";
    internal const string DefaultMediaTarget = "/media";
    internal const string FontsTarget = "/usr/local/share/fonts/custom";

    internal const string PublishedServerUrlEnvVar = "JELLYFIN_PublishedServerUrl";

    private EndpointReference? _primaryEndpoint;

    /// <summary>
    /// Gets the primary HTTP endpoint for the Jellyfin server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new EndpointReference(this, HttpEndpointName);

    /// <summary>
    /// Gets the host endpoint reference for the HTTP endpoint.
    /// </summary>
    public EndpointReferenceExpression Host => PrimaryEndpoint.Property(EndpointProperty.Host);

    /// <summary>
    /// Gets the port endpoint reference for the HTTP endpoint.
    /// </summary>
    public EndpointReferenceExpression Port => PrimaryEndpoint.Property(EndpointProperty.Port);

    /// <summary>
    /// Gets the connection URI expression for the Jellyfin HTTP endpoint.
    /// </summary>
    /// <remarks>
    /// Format: <c>http://{host}:{port}</c>.
    /// </remarks>
    public ReferenceExpression UriExpression => ReferenceExpression.Create($"{PrimaryEndpoint.Scheme}://{Host}:{Port}");

    /// <summary>
    /// Connection string for the Jellyfin server in the form <c>Endpoint=http://host:port</c>.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create(
        $"Endpoint={PrimaryEndpoint.Scheme}://{Host}:{Port}");

    IEnumerable<KeyValuePair<string, ReferenceExpression>> IResourceWithConnectionString.GetConnectionProperties()
    {
        yield return new("Host", ReferenceExpression.Create($"{Host}"));
        yield return new("Port", ReferenceExpression.Create($"{Port}"));
        yield return new("Uri", UriExpression);
    }
}

#pragma warning restore ASPIREATS001
