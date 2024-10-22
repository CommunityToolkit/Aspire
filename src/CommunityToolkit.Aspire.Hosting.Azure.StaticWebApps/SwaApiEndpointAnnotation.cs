namespace Aspire.Hosting.ApplicationModel;

public class SwaApiEndpointAnnotation(IResourceBuilder<IResourceWithEndpoints> resource) : IResourceAnnotation
{
    public string Endpoint => resource.Resource.GetEndpoint("http").Url;
}