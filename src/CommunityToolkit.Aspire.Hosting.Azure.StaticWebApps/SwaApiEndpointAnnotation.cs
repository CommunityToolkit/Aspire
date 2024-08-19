using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps;

public class SwaApiEndpointAnnotation(IResourceBuilder<IResourceWithEndpoints> resource) : IResourceAnnotation
{
    public string Endpoint => resource.Resource.GetEndpoint("http").Url;
}