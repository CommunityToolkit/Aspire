using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

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
    [Obsolete("Use the overload with McpInspectorOptions instead. This overload will be removed in the next version.")]
    public static IResourceBuilder<McpInspectorResource> AddMcpInspector(this IDistributedApplicationBuilder builder, [ResourceName] string name, int clientPort = 6274, int serverPort = 6277, string inspectorVersion = McpInspectorResource.InspectorVersion)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return AddMcpInspector(builder, name, options =>
        {
            options.ClientPort = clientPort;
            options.ServerPort = serverPort;
            options.InspectorVersion = inspectorVersion;
        });
    }

    /// <summary>
    /// Adds a MCP Inspector container resource to the <see cref="IDistributedApplicationBuilder"/> using an options object.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the MCP Inspector resource will be added.</param>
    /// <param name="name">The name of the MCP Inspector container resource.</param>
    /// <param name="options">The <see cref="McpInspectorOptions"/> to configure the MCP Inspector resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{McpInspectorResource}"/> for further configuration.</returns>
    public static IResourceBuilder<McpInspectorResource> AddMcpInspector(this IDistributedApplicationBuilder builder, [ResourceName] string name, McpInspectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        var proxyTokenParameter = options.ProxyToken?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-proxyToken");

        var resource = builder.AddResource(new McpInspectorResource(name))
            .WithArgs(["-y", $"@modelcontextprotocol/inspector@{options.InspectorVersion}"])
            .ExcludeFromManifest()
            .WithHttpEndpoint(isProxied: false, port: options.ClientPort, env: "CLIENT_PORT", name: McpInspectorResource.ClientEndpointName)
            .WithHttpEndpoint(isProxied: false, port: options.ServerPort, env: "SERVER_PORT", name: McpInspectorResource.ServerProxyEndpointName)
            .WithHttpHealthCheck("/", endpointName: McpInspectorResource.ClientEndpointName)
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
                annotation.DisplayLocation = UrlDisplayLocation.DetailsOnly;
            })
            .OnBeforeResourceStarted(async (inspectorResource, @event, ct) =>
            {
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
            })
            .WithEnvironment(ctx =>
            {
                if (ctx.Resource is not McpInspectorResource resource)
                {
                    return;
                }

                var clientEndpoint = resource.GetEndpoint(McpInspectorResource.ClientEndpointName);
                var serverProxyEndpoint = resource.GetEndpoint(McpInspectorResource.ServerProxyEndpointName);

                if (clientEndpoint is null || serverProxyEndpoint is null)
                {
                    throw new InvalidOperationException("The MCP Inspector resource must have both 'client' and 'server-proxy' endpoints defined.");
                }

                ctx.EnvironmentVariables["MCP_PROXY_FULL_ADDRESS"] = serverProxyEndpoint.Url;
                ctx.EnvironmentVariables["CLIENT_PORT"] = clientEndpoint.TargetPort?.ToString() ?? throw new InvalidOperationException("The MCP Inspector 'client' endpoint must have a target port defined.");
                ctx.EnvironmentVariables["SERVER_PORT"] = serverProxyEndpoint.TargetPort?.ToString() ?? throw new InvalidOperationException("The MCP Inspector 'server-proxy' endpoint must have a target port defined.");
                ctx.EnvironmentVariables["MCP_PROXY_AUTH_TOKEN"] = proxyTokenParameter;
            })
            .WithDefaultArgs()
            .WithUrls(async context =>
            {
                var token = await proxyTokenParameter.GetValueAsync(CancellationToken.None);

                foreach (var url in context.Urls)
                {
                    if (url.Endpoint is not null)
                    {
                        var uriBuilder = new UriBuilder(url.Url)
                        {
                            Query = $"MCP_PROXY_AUTH_TOKEN={Uri.EscapeDataString(token!)}"
                        };
                        url.Url = uriBuilder.ToString();
                    }
                }
            });

        resource.Resource.ProxyTokenParameter = proxyTokenParameter;

        // Add authenticated health check for server proxy /config endpoint
        var healthCheckKey = $"{name}_proxy_config_check";
        builder.Services.AddHealthChecks().AddUrlGroup(options =>
        {
            var serverProxyEndpoint = resource.GetEndpoint(McpInspectorResource.ServerProxyEndpointName);
            var uri = serverProxyEndpoint.Url ?? throw new DistributedApplicationException("The MCP Inspector 'server-proxy' endpoint URL is not set. Ensure that the resource has been allocated before the health check is executed.");
            var healthCheckUri = new Uri(new Uri(uri), "/config");
            options.AddUri(healthCheckUri, async setup =>
            {
                var token = await proxyTokenParameter.GetValueAsync(CancellationToken.None);
                setup.AddCustomHeader("X-MCP-Proxy-Auth", $"Bearer {token}");
            });
        }, healthCheckKey);

        return resource.WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Adds a MCP Inspector container resource to the <see cref="IDistributedApplicationBuilder"/> using a configuration delegate.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the MCP Inspector resource will be added.</param>
    /// <param name="name">The name of the MCP Inspector container resource.</param>
    /// <param name="configureOptions">A delegate to configure the <see cref="McpInspectorOptions"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{McpInspectorResource}"/> for further configuration.</returns>
    public static IResourceBuilder<McpInspectorResource> AddMcpInspector(this IDistributedApplicationBuilder builder, [ResourceName] string name, Action<McpInspectorOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var options = new McpInspectorOptions();
        configureOptions(options);

        return builder.AddMcpInspector(name, options);
    }

    /// <summary>
    /// Adds a MCP Inspector container resource to the <see cref="IDistributedApplicationBuilder"/> using a configuration delegate.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the MCP Inspector resource will be added.</param>
    /// <param name="name">The name of the MCP Inspector container resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{McpInspectorResource}"/> for further configuration.</returns>
    public static IResourceBuilder<McpInspectorResource> AddMcpInspector(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new McpInspectorOptions();

        return builder.AddMcpInspector(name, options);
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

    private static IResourceBuilder<McpInspectorResource> WithDefaultArgs(this IResourceBuilder<McpInspectorResource> builder)
    {
        return builder
            .WithArgs(ctx =>
            {
                McpInspectorResource inspectorResource = builder.Resource;
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
}
