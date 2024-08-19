using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Azure.StaticWebApps;

public class SwaAppEndpointAnnotation(IResourceBuilder<IResourceWithEndpoints> resource) : IResourceAnnotation
{
    public string Endpoint => resource.Resource.GetEndpoint("http").Url;
}
