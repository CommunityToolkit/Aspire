using Aspire.Hosting.Python;

namespace Aspire.Hosting.ApplicationModel;

public class UvicornAppResource(string name, string workingDirectory)
    : PythonAppResource(name, "uvicorn", workingDirectory), IResourceWithServiceDiscovery;