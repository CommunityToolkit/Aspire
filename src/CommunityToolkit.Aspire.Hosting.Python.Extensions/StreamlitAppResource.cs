using Aspire.Hosting.Python;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Streamlit application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="workingDirectory">The working directory for streamlit.</param>
public class StreamlitAppResource(string name, string workingDirectory)
    : PythonAppResource(name, "streamlit", workingDirectory), IResourceWithServiceDiscovery;
