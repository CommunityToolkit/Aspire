using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.CommunityToolkit.Azure.Hosting.DataApiBuilder;

/// <summary>
/// A resource that represents a generated API by Data Api Builder.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <param name="command">The command to execute.</param>
/// <param name="workingDirectory">The working directory to use for the command. If null, the working directory of the current process is used.</param>
public class DataApiBuilderExecutableResource(string name, string command, string workingDirectory)
    : ExecutableResource(name, command, workingDirectory), IResourceWithServiceDiscovery
{
    internal const string HttpEndpointName = "http";
}
