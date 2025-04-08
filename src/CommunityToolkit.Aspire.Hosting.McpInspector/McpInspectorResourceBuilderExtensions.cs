using Aspire.Hosting.ApplicationModel;
#pragma warning disable CTASPIRE003

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding the MCP Inspector to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class McpInspectorResourceBuilderExtensions
{
    /// <summary>
    /// Adds a MCP Inspector container resource to the <see cref="IDistributedApplicationBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the MCP Inspector resource will be added.</param>
    /// <param name="name">The name of the MCP Inspector container resource.</param>
    public static IResourceBuilder<McpInspectorResource> AddMcpInspector(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        var resource = builder.AddResource(new McpInspectorResource(name))
            .WithArgs(["-y", "@modelcontextprotocol/inspector"])
            .ExcludeFromManifest()
            .WithHttpEndpoint(isProxied: false, port: Random.Shared.Next(3000, 4000), env: "CLIENT_PORT", name: "client")
            .WithHttpEndpoint(isProxied: false, port: Random.Shared.Next(4000, 5000), env: "SERVER_PORT", name: "server-proxy");

        return resource
            .WithEnvironment(ctx =>
            {
                ctx.EnvironmentVariables["MCP_PROXY_FULL_ADDRESS"] = resource.GetEndpoint("server-proxy");
            });
    }

    /// <summary>
    /// Configures the MCP Inspector resource to use a specified MCP server resource that uses SSE as the transport type.
    /// </summary>
    /// <typeparam name="TResource">The type of the MCP server resource.</typeparam>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the MCP Inspector resource.</param>
    /// <param name="mcpServer">The <see cref="IResourceBuilder{T}"/> for the MCP server resource.</param>
    /// <param name="route">The route that the SSE connection will use.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{McpInspectorResource}"/> for further configuration.</returns>
    public static IResourceBuilder<McpInspectorResource> WithMcpServer<TResource>(this IResourceBuilder<McpInspectorResource> builder, IResourceBuilder<TResource> mcpServer, string route = "/sse")
        where TResource : IResourceWithEndpoints
    {
        return builder.WithArgs(ctx =>
        {
            var httpEndpoint = mcpServer.Resource.GetEndpoint("http");

            var url = ReferenceExpression.Create($"{httpEndpoint}{route}");
            ctx.Args.Add(url);
        });
    }
}