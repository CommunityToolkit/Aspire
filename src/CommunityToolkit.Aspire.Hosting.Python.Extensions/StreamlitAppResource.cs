using Aspire.Hosting.Python;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Streamlit application.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="executablePath">The path to the executable used to run the Streamlit app.</param>
/// <param name="workingDirectory">The working directory for streamlit.</param>
public class StreamlitAppResource(string name, string executablePath, string workingDirectory)
    : PythonAppResource(name, executablePath, workingDirectory), IResourceWithServiceDiscovery;
