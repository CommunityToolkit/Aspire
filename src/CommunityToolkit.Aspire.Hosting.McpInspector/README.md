# CommunityToolkit.Aspire.Hosting.McpInspector library

Provides extension methods and resource definitions for the Aspire AppHost to support adding an MCP Inspector resource. The MCP Inspector enables inspection and debugging of MCP (Model Context Protocol) servers, with support for multiple server configurations and transport types.

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

You can specify the transport type (`StreamableHttp`) and set which server is the default for the inspector.

#### Using alternative package managers

By default, the MCP Inspector uses npm/npx. You can configure it to use yarn, pnpm, or bun instead by chaining the appropriate method:

```csharp
// Using yarn
var inspector = builder.AddMcpInspector("inspector")
    .WithYarn()
    .WithMcpServer(mcpServer);

// Using pnpm
var inspector = builder.AddMcpInspector("inspector")
    .WithPnpm()
    .WithMcpServer(mcpServer);

// Using bun
var inspector = builder.AddMcpInspector("inspector")
    .WithBun()
    .WithMcpServer(mcpServer);
```

When using yarn, pnpm, or bun, the inspector will use `yarn dlx`, `pnpm dlx`, or `bunx` respectively to run the MCP Inspector package.

#### Using options for complex configurations

For more complex configurations with multiple parameters, you can use the options-based approach:

```csharp
var customToken = builder.AddParameter("custom-proxy-token", secret: true);

var options = new McpInspectorOptions
{
    ClientPort = 6275,
    ServerPort = 6278,
    InspectorVersion = "0.16.2",
    ProxyToken = customToken
};

var inspector = builder.AddMcpInspector("inspector", options)
    .WithMcpServer(mcpServer);
```

Alternatively, you can use a configuration delegate for a more fluent approach:

```csharp
var inspector = builder.AddMcpInspector("inspector", options =>
{
    options.ClientPort = 6275;
    options.ServerPort = 6278;
    options.InspectorVersion = "0.16.2";
})
    .WithMcpServer(mcpServer);
```

#### Configuration options

The `McpInspectorOptions` class provides the following configuration properties:

-   `ClientPort`: Port for the client application (default: 6274)
-   `ServerPort`: Port for the server proxy application (default: 6277)
-   `InspectorVersion`: Version of the Inspector app to use (default: latest supported version)
-   `ProxyToken`: Custom authentication token parameter (default: auto-generated)

## Additional Information

See the [official documentation](https://learn.microsoft.com/dotnet/aspire/community-toolkit/mcpinspector) for more details.

## Feedback & contributing

[https://github.com/CommunityToolkit/Aspire](https://github.com/CommunityToolkit/Aspire)
