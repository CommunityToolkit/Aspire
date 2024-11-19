using Aspire.Hosting.Python;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Uvicorn application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory for uvicorn.</param>
public class UvicornAppResource(string name, string workingDirectory)
    : PythonAppResource(name, "uvicorn", workingDirectory), IResourceWithServiceDiscovery;