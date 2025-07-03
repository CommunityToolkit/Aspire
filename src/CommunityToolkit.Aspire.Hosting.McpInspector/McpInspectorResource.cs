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
    /// Gets the version of the MCP Inspector.
    /// </summary>
    public const string InspectorVersion = "0.15.0";

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
