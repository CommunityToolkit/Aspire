using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Contribs.Hosting.Java;

/// <summary>
/// A resource that represents a Java application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="entrypoint">An optional container entrypoint.</param>

public class JavaAppContainerResource(string name, string? entrypoint = null)
    : ContainerResource(name, entrypoint), IResourceWithServiceDiscovery
{
    internal const string HttpEndpointName = "http";
}
