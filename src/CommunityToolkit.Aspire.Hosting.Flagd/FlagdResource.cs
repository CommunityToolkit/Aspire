namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a flagd container.
/// </summary>
/// <remarks>
/// Constructs a <see cref="FlagdResource"/>.
/// </remarks>
/// <param name="name">The name of the resource.</param>
public class FlagdResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";
    internal const string GrpcEndpointName = "grpc";
    
    private readonly List<string> _flagSources = [];
    
    private EndpointReference? _primaryEndpointReference;

    /// <summary>
    /// Gets the list of flag sources (URIs) configured for this flagd instance.
    /// </summary>
    public IReadOnlyList<string> FlagSources => _flagSources;

    /// <summary>
    /// Gets the primary HTTP endpoint for the flagd server.
    /// </summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpointReference ??= new(this, HttpEndpointName);

    /// <summary>
    /// Gets the connection string expression for the flagd server.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}"
        );

    /// <summary>
    /// Adds a flag source URI to the list of sources for flagd to monitor.
    /// </summary>
    /// <param name="uri">The URI of the flag source (e.g., file:///etc/flagd/flags.json, http://example.com/flags.json).</param>
    public void AddFlagSource(string uri)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri, nameof(uri));
        if (!_flagSources.Contains(uri))
        {
            _flagSources.Add(uri);
        }
    }
}
