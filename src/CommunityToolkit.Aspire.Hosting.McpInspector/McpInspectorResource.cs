using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the MCP Inspector server.
/// </summary>
/// <param name="name">The name of the resource.</param>
public class McpInspectorResource(string name) : ExecutableResource(name, "npx", "")
{
    internal readonly string ConfigPath = Path.GetTempFileName();

    /// <summary>
    /// The name of the client endpoint.
    /// </summary>
    public const string ClientEndpointName = "client";

    private EndpointReference? _clientEndpoint;

    /// <summary>
    /// Gets the client endpoint reference for the MCP Inspector.
    /// </summary>
    public EndpointReference ClientEndpoint => _clientEndpoint ??= new(this, ClientEndpointName);

    /// <summary>
    /// The name of the server proxy endpoint.
    /// </summary>
    public const string ServerProxyEndpointName = "server-proxy";

    private EndpointReference? _serverProxyEndpoint;

    /// <summary>
    /// Gets the server proxy endpoint reference for the MCP Inspector.
    /// </summary>
    public EndpointReference ServerProxyEndpoint => _serverProxyEndpoint ??= new(this, ServerProxyEndpointName);

    /// <summary>
    /// Gets the version of the MCP Inspector.
    /// </summary>
    public const string InspectorVersion = "0.16.2";

    private readonly List<McpServerMetadata> _mcpServers = [];

    private McpServerMetadata? _defaultMcpServer;

    /// <summary>
    /// List of MCP server resources that this inspector is aware of.
    /// </summary>
    public IReadOnlyList<McpServerMetadata> McpServers => _mcpServers;

    /// <summary>
    /// Gets the default MCP server resource.
    /// </summary>
    public McpServerMetadata? DefaultMcpServer => _defaultMcpServer;

    /// <summary>
    /// Gets or sets the parameter that contains the MCP proxy authentication token.
    /// </summary>
    public ParameterResource ProxyTokenParameter { get; set; } = default!;

    internal void AddMcpServer(IResourceWithEndpoints mcpServer, bool isDefault, McpTransportType transportType)
    {
        if (_mcpServers.Any(s => s.Name == mcpServer.Name))
        {
            throw new InvalidOperationException($"The MCP server {mcpServer.Name} is already added to the MCP Inspector resource.");
        }

        McpServerMetadata item = new(
            mcpServer.Name,
            mcpServer.GetEndpoint("http") ?? throw new InvalidOperationException($"The MCP server {mcpServer.Name} must have a 'http' endpoint defined."),
            transportType);

        _mcpServers.Add(item);

        if (isDefault)
        {
            _defaultMcpServer = item;
        }
    }
}
