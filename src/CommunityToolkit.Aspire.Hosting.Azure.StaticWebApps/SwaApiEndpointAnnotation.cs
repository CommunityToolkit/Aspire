namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an annotation for an API endpoint in a Static Web App.
/// </summary>
/// <param name="resource">The resource builder for resources with endpoints.</param>
[Obsolete(
    message: "The SWA emulator integration is going to be removed in a future release.",
    error: false,
    DiagnosticId = "CTASPIRE003",
    UrlFormat = "https://github.com/CommunityToolit/aspire/issues/698")]
public class SwaApiEndpointAnnotation(IResourceBuilder<IResourceWithEndpoints> resource) : IResourceAnnotation
{
    /// <summary>
    /// Gets the endpoint URL for the resource.
    /// </summary>
    public string Endpoint => resource.Resource.GetEndpoint("http").Url;
}