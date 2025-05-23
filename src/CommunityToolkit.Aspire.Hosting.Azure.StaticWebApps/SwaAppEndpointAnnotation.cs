namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents an annotation for an endpoint in a Static Web App.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SwaAppEndpointAnnotation"/> class.
/// </remarks>
/// <param name="resource">The resource builder for the endpoint.</param>
[Obsolete(
    message: "The SWA emulator integration is going to be removed in a future release.",
    error: false,
    DiagnosticId = "CTASPIRE003",
    UrlFormat = "https://github.com/CommunityToolit/aspire/issues/698")]
public class SwaAppEndpointAnnotation(IResourceBuilder<IResourceWithEndpoints> resource) : IResourceAnnotation
{
    /// <summary>
    /// Gets the HTTP endpoint URL.
    /// </summary>
    public string Endpoint => resource.Resource.GetEndpoint("http").Url;
}
