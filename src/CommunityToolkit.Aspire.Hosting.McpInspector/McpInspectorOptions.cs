using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Options for configuring the MCP Inspector resource.
/// </summary>
public class McpInspectorOptions
{
    /// <summary>
    /// Gets or sets the port for the client application. Defaults to "6274".
    /// </summary>
    public int ClientPort { get; set; } = 6274;

    /// <summary>
    /// Gets or sets the port for the server proxy application. Defaults to "6277".
    /// </summary>
    public int ServerPort { get; set; } = 6277;

    /// <summary>
    /// Gets or sets the version of the Inspector app to use. Defaults to <see cref="McpInspectorResource.InspectorVersion"/>.
    /// </summary>
    public string InspectorVersion { get; set; } = McpInspectorResource.InspectorVersion;

    /// <summary>
    /// Gets or sets the parameter used to provide the proxy authentication token for the MCP Inspector resource. 
    /// If <see langword="null"/> a random token will be generated.
    /// </summary>
    public IResourceBuilder<ParameterResource>? ProxyToken { get; set; }
}
