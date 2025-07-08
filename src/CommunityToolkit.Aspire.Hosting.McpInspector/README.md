# CommunityToolkit.Aspire.Hosting.McpInspector library

Provides extension methods and resource definitions for the .NET Aspire AppHost to support adding an MCP Inspector resource. The MCP Inspector enables inspection and debugging of MCP (Model Context Protocol) servers, with support for multiple server configurations and transport types.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.McpInspector
```

### Example usage

In your `Program.cs`, add an MCP Inspector resource and configure it with one or more MCP servers:

```csharp
var mcpServer = builder.AddProject<Projects.McpServer>("mcp-server");

var inspector = builder.AddMcpInspector("inspector")
    .WithMcpServer(mcpServer);
```

You can specify the transport type (`StreamableHttp` or `Sse`) and set which server is the default for the inspector.

## Additional Information

See the [official documentation](https://learn.microsoft.com/dotnet/aspire/community-toolkit/mcpinspector) for more details.

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
