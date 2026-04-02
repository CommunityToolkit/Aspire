#pragma warning disable ASPIREATS001

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// A resource that represents a Java application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="entrypoint">An optional container entrypoint.</param>

[global::Aspire.Hosting.AspireExport(ExposeProperties = true)]
public class JavaAppContainerResource(string name, string? entrypoint = null)
    : ContainerResource(name, entrypoint), IResourceWithServiceDiscovery
{
    internal const string HttpEndpointName = "http";
}

#pragma warning restore ASPIREATS001
