namespace Aspire.Hosting;

/// <summary>
/// Represents the type of transport used by the MCP server.
/// </summary>
public enum McpTransportType
{
    /// <summary>
    /// The MCP server uses SSE (Server-Sent Events) as the transport type.
    /// </summary>
    StreamableHttp,

    /// <summary>
    /// The MCP server uses Server Sent Events (SSE) as the transport type.
    /// </summary>
    Sse
}