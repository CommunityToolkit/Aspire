namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a ngrok endpoint annotation for an existing resource with endpoints.
/// </summary>
/// <param name="Resource">The resource with endpoints to tunnel.</param>
public sealed record NgrokEndpointAnnotation(IResourceWithEndpoints Resource) : IResourceAnnotation
{
    /// <summary>
    /// Gets the collection of endpoints to tunnel
    /// </summary>
    public ICollection<NgrokEndpoint> Endpoints { get; } = new HashSet<NgrokEndpoint>();
}