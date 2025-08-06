using Aspire.Hosting.ApplicationModel;

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
    /// <param name="clientPort">The port for the client application. Defaults to 6274.</param>
    /// <param name="serverPort">The port for the server proxy application. Defaults to 6277.</param>
    /// <param name="inspectorVersion">The version of the Inspector app to use</param>
    public static IResourceBuilder<McpInspectorResource> AddMcpInspector(this IDistributedApplicationBuilder builder, [ResourceName] string name, int clientPort = 6274, int serverPort = 6277, string inspectorVersion = McpInspectorResource.InspectorVersion)
    {
        var resource = builder.AddResource(new McpInspectorResource(name))
            .WithArgs(["-y", $"@modelcontextprotocol/inspector@{inspectorVersion}"])
            .ExcludeFromManifest()
            .WithHttpEndpoint(isProxied: false, port: clientPort, env: "CLIENT_PORT", name: McpInspectorResource.ClientEndpointName)
            .WithHttpEndpoint(isProxied: false, port: serverPort, env: "SERVER_PORT", name: McpInspectorResource.ServerProxyEndpointName)
            .WithEnvironment("DANGEROUSLY_OMIT_AUTH", "true")
            .WithHttpHealthCheck("/", endpointName: McpInspectorResource.ClientEndpointName)
            .WithHttpHealthCheck("/config", endpointName: McpInspectorResource.ServerProxyEndpointName)
            .WithEnvironment("MCP_AUTO_OPEN_ENABLED", "false")
            .WithUrlForEndpoint(McpInspectorResource.ClientEndpointName, annotation =>
            {
                annotation.DisplayText = "Client";
                annotation.DisplayOrder = 2;
            })
            .WithUrlForEndpoint(McpInspectorResource.ServerProxyEndpointName, annotation =>
            {
                annotation.DisplayText = "Server Proxy";
                annotation.DisplayOrder = 1;
            });

        builder.Eventing.Subscribe<BeforeResourceStartedEvent>(resource.Resource, async (@event, ct) =>
        {
            if (@event.Resource is not McpInspectorResource inspectorResource)
            {
                return;
            }

            if (inspectorResource.DefaultMcpServer is null && inspectorResource.McpServers.Count > 0)
            {
                throw new InvalidOperationException("No default MCP server has been configured for the MCP Inspector resource, yet servers have been provided.");
            }

            var servers = inspectorResource.McpServers.ToDictionary(s => s.Name, s => new
            {
                transport = s.TransportType switch
                {
                    McpTransportType.StreamableHttp => "streamable-http",
#pragma warning disable CS0618
                    McpTransportType.Sse => "sse",
#pragma warning restore CS0618
                    _ => throw new NotSupportedException($"The transport type {s.TransportType} is not supported.")
                },
                endpoint = s.Endpoint.Url
            });

            var config = new { mcpServers = servers };

            await File.WriteAllTextAsync(inspectorResource.ConfigPath, System.Text.Json.JsonSerializer.Serialize(config), ct);
        });

        return resource
            .WithEnvironment(ctx =>
            {
                var clientEndpoint = resource.GetEndpoint(McpInspectorResource.ClientEndpointName);
                var serverProxyEndpoint = resource.GetEndpoint(McpInspectorResource.ServerProxyEndpointName);

                if (clientEndpoint is null || serverProxyEndpoint is null)
                {
                    throw new InvalidOperationException("The MCP Inspector resource must have both 'client' and 'server-proxy' endpoints defined.");
                }

                ctx.EnvironmentVariables["MCP_PROXY_FULL_ADDRESS"] = serverProxyEndpoint.Url;
                ctx.EnvironmentVariables["CLIENT_PORT"] = clientEndpoint.TargetPort?.ToString() ?? throw new InvalidOperationException("The MCP Inspector 'client' endpoint must have a target port defined.");
                ctx.EnvironmentVariables["SERVER_PORT"] = serverProxyEndpoint.TargetPort?.ToString() ?? throw new InvalidOperationException("The MCP Inspector 'server-proxy' endpoint must have a target port defined.");
            })
            .WithArgs(ctx =>
            {
                McpInspectorResource inspectorResource = resource.Resource;
                McpServerMetadata? defaultMcpServer = inspectorResource.DefaultMcpServer;
                if ((defaultMcpServer is null && inspectorResource.McpServers.Count > 0) || (defaultMcpServer is not null && inspectorResource.McpServers.Count == 0))
                {
                    throw new InvalidOperationException("No default MCP server has been configured for the MCP Inspector resource, yet servers have been provided.");
                }


                if (defaultMcpServer is null && inspectorResource.McpServers.Count == 0)
                {
                    return;
                }

                ctx.Args.Add("--config");
                ctx.Args.Add(inspectorResource.ConfigPath);
                ctx.Args.Add("--server");
                ctx.Args.Add(defaultMcpServer?.Name ?? throw new InvalidOperationException("The MCP Inspector resource must have a default MCP server defined."));
            });
    }

    /// <summary>
    /// Configures the MCP Inspector resource to use a specified MCP server resource that uses SSE as the transport type.
    /// </summary>
    /// <typeparam name="TResource">The type of the MCP server resource.</typeparam>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the MCP Inspector resource.</param>
    /// <param name="mcpServer">The <see cref="IResourceBuilder{T}"/> for the MCP server resource.</param>
    /// <param name="isDefault">Indicates whether this MCP server should be considered the default server for the MCP Inspector.</param>
    /// <param name="transportType">The transport type to use for the MCP server. Defaults to <see cref="McpTransportType.StreamableHttp"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{McpInspectorResource}"/> for further configuration.</returns>
    public static IResourceBuilder<McpInspectorResource> WithMcpServer<TResource>(
        this IResourceBuilder<McpInspectorResource> builder,
        IResourceBuilder<TResource> mcpServer,
        bool isDefault = true,
        McpTransportType transportType = McpTransportType.StreamableHttp)
        where TResource : IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(mcpServer);
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.AddMcpServer(mcpServer.Resource, isDefault, transportType);
        return builder;
    }
}
