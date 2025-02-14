using Aspire.Hosting.Python;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Uv application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="executablePath">The path to the executable used to run the python app.</param>
/// <param name="workingDirectory">The working directory for uv.</param>
public class UvAppResource(string name, string executablePath, string workingDirectory)
    : PythonAppResource(name, executablePath, workingDirectory), IResourceWithServiceDiscovery
{
    internal const string HttpEndpointName = "http";
}
