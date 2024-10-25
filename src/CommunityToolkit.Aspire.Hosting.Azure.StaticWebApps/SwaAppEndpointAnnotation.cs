namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an annotation for an endpoint in a Static Web App.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SwaAppEndpointAnnotation"/> class.
/// </remarks>
/// <param name="resource">The resource builder for the endpoint.</param>
public class SwaAppEndpointAnnotation(IResourceBuilder<IResourceWithEndpoints> resource) : IResourceAnnotation
{
    /// <summary>
    /// Gets the HTTP endpoint URL.
    /// </summary>
    public string Endpoint => resource.Resource.GetEndpoint("http").Url;
}
