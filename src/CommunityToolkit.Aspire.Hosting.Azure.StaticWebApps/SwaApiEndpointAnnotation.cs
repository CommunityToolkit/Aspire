namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an annotation for an API endpoint in a Static Web App.
/// </summary>
/// <param name="resource">The resource builder for resources with endpoints.</param>
public class SwaApiEndpointAnnotation(IResourceBuilder<IResourceWithEndpoints> resource) : IResourceAnnotation
{
    /// <summary>
    /// Gets the endpoint URL for the resource.
    /// </summary>
    public string Endpoint => resource.Resource.GetEndpoint("http").Url;
}