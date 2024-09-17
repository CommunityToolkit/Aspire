namespace Aspire.Hosting.ApplicationModel;

public class SwaAppEndpointAnnotation(IResourceBuilder<IResourceWithEndpoints> resource) : IResourceAnnotation
{
    public string Endpoint => resource.Resource.GetEndpoint("http").Url;
}
