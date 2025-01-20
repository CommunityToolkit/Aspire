using Aspire.Hosting.Python;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Uv application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory for uv.</param>
public class UvAppResource(string name, string workingDirectory)
    : PythonAppResource(name, "uv", workingDirectory), IResourceWithServiceDiscovery
{
    internal const string HttpEndpointName = "http";
}
