namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents metadata for an MCP server used by the MCP Inspector.
/// </summary>
/// <param name="Name">The name of the server resource.</param>
/// <param name="Endpoint">The endpoint reference for the server resource.</param>
/// <param name="TransportType">The transport type used by the server resource.</param>
/// <param name="Path">The path used for MCP communication.</param>
public record McpServerMetadata(string Name, EndpointReference Endpoint, McpTransportType TransportType, string Path);