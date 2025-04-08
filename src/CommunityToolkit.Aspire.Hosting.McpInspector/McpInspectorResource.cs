using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Resource for the MCP Inspector server.
/// </summary>
/// <param name="name">The name of the resource.</param>
/// <remarks>
/// This resource will run as a Node.js process using the npx command.
/// 
/// In future, it is likely to become a container resource, once <seealso href="https://github.com/modelcontextprotocol/inspector/issues/237"/> is resolved.
/// </remarks>
[Experimental("CTASPIRE003")]
public class McpInspectorResource(string name) : ExecutableResource(name, "npx", "");
