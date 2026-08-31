using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci;

namespace Aspire.Hosting;

public static partial class FlociHostingExtension
{
    /// <summary>
    /// Adds a Floci Azure emulator container resource to the <see cref="IDistributedApplicationBuilder"/>.
    /// </summary>
    /// <ats-summary>Adds a Floci Azure emulator container resource</ats-summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the Floci resource will be added.</param>
    /// <param name="name">The name of the Floci container resource.</param>
    /// <param name="port">Optional. The host port to bind for the Azure endpoint.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociAzureContainerResource}"/> for further resource configuration.</returns>
    [AspireExport]
    public static IResourceBuilder<FlociAzureContainerResource> AddFlociAzure(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        FlociAzureContainerResource resource = new(name);

        var flociBuilder = builder.AddResource(resource)
            .WithImage(FlociContainerImageTags.AzureImage)
            .WithImageTag(FlociContainerImageTags.AzureTag)
            .WithImageRegistry(FlociContainerImageTags.AzureRegistry)
            .WithHttpEndpoint(
                targetPort: FlociAzureContainerResource.EndpointPort,
                port: port,
                name: resource.EndpointName)
            .WithEnvironment(FlociAzureContainerResource.HostnameEnvVar, name)
            .WithEnvironment(resource.StorageModeEnvVar, "memory")
            .WithHttpHealthCheck(
                path: "/_floci/health",
                statusCode: 200,
                endpointName: resource.EndpointName);

        ConfigureTlsCore(builder, flociBuilder);

        return flociBuilder;
    }

    /// <summary>
    /// Adds a reference to a Floci Azure emulator resource, injecting the standard
    /// <c>ConnectionStrings__{name}</c> entry plus <c>AZURE_STORAGE_CONNECTION_STRING</c> — a
    /// development storage connection string carrying the <c>Blob</c>, <c>Queue</c> and <c>Table</c>
    /// endpoints so all three Azure Storage SDK clients resolve to the emulator.
    /// </summary>
    /// <ats-summary>Adds a reference to a Floci Azure emulator resource</ats-summary>
    /// <typeparam name="TDestination">The type of the resource receiving the reference.</typeparam>
    /// <param name="builder">The resource builder for the resource receiving the reference.</param>
    /// <param name="floci">The Floci Azure resource to reference.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TDestination}"/> for further configuration.</returns>
    [AspireExport("withFlociAzureReference")]
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<FlociAzureContainerResource> floci)
        where TDestination : IResourceWithEnvironment
        => WithFlociReferenceCore(builder, floci, static (context, resource, endpoint) =>
        {
            // floci-az serves Blob, Queue and Table from the same port, so all three endpoints
            // share one URL. Omitting Queue/Table would leave those SDK clients falling back to
            // the public core.windows.net endpoints.
            var account = FlociAzureContainerResource.DefaultAccountName;
            var serviceEndpoint = ReferenceExpression.Create($"{endpoint.Url}/{account}");

            context.EnvironmentVariables["AZURE_STORAGE_CONNECTION_STRING"] = ReferenceExpression.Create(
                $"DefaultEndpointsProtocol={resource.Scheme};AccountName={account};AccountKey={FlociAzureContainerResource.DefaultAccountKey};BlobEndpoint={serviceEndpoint};QueueEndpoint={serviceEndpoint};TableEndpoint={serviceEndpoint};");

        });

    /// <summary>
    /// Adds a child resource representing the Service Bus AMQP data plane exposed by the Floci
    /// Azure emulator, enabling the data plane on the emulator (<c>MOCKED=false</c>,
    /// <c>START_ON_BOOT=true</c>) and pinning the host ports its Artemis sidecar publishes.
    /// </summary>
    /// <remarks>
    /// Reference the returned resource with Aspire's standard <c>WithReference</c> API to inject
    /// its Service Bus connection string (e.g. for <c>AddAzureServiceBusClient</c>). The Artemis
    /// sidecar is a separate container floci-az starts via Docker, so the emulator also needs
    /// <see cref="WithDockerSocket(IResourceBuilder{FlociAzureContainerResource}, string)"/>.
    /// When no ports are passed, free host ports are allocated so concurrent AppHosts don't
    /// collide. Requires a floci-az release with start-on-boot support (floci-io/floci-az#249).
    /// </remarks>
    /// <ats-summary>Adds a Service Bus child resource to the Floci Azure emulator</ats-summary>
    /// <param name="builder">The Floci Azure resource builder.</param>
    /// <param name="name">The name of the Service Bus resource (default: <c>servicebus</c>).</param>
    /// <param name="amqpPort">Host port for plain AMQP (default: a free port).</param>
    /// <param name="amqpTlsPort">Host port for AMQPS/TLS (default: a free port).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociAzureServiceBusResource}"/> for further configuration.</returns>
    [AspireExport]
    public static IResourceBuilder<FlociAzureServiceBusResource> WithServiceBus(
        this IResourceBuilder<FlociAzureContainerResource> builder,
        [ResourceName] string name = FlociAzureServiceBusResource.DefaultName,
        int? amqpPort = null,
        int? amqpTlsPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // One Service Bus child per emulator — the ports configure the parent container's
        // environment, so a second child could not have different ones.
        FlociAzureServiceBusResource? existing = builder.ApplicationBuilder.Resources
            .OfType<FlociAzureServiceBusResource>()
            .FirstOrDefault(resource => resource.Parent == builder.Resource);
        if (existing is not null)
        {
            if ((amqpPort is not null && amqpPort != existing.AmqpPort)
                || (amqpTlsPort is not null && amqpTlsPort != existing.AmqpTlsPort))
            {
                throw new InvalidOperationException(
                    $"Service Bus is already configured on '{builder.Resource.Name}' with AMQP port {existing.AmqpPort} (TLS {existing.AmqpTlsPort}) and cannot be reconfigured with different ports.");
            }
            return builder.ApplicationBuilder.CreateResourceBuilder(existing);
        }

        (int fallbackAmqpPort, int fallbackAmqpTlsPort) = GetFreeTcpPorts();
        FlociAzureServiceBusResource serviceBus = new(
            name, amqpPort ?? fallbackAmqpPort, amqpTlsPort ?? fallbackAmqpTlsPort, builder.Resource);

        builder
            .WithEnvironment("FLOCI_AZ_SERVICES_SERVICE_BUS_MOCKED", "false")
            .WithEnvironment("FLOCI_AZ_SERVICES_SERVICE_BUS_START_ON_BOOT", "true")
            .WithEnvironment("FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_PORT",
                serviceBus.AmqpPort.ToString(CultureInfo.InvariantCulture))
            .WithEnvironment("FLOCI_AZ_SERVICES_SERVICE_BUS_AMQP_TLS_PORT",
                serviceBus.AmqpTlsPort.ToString(CultureInfo.InvariantCulture));

        return builder.ApplicationBuilder
            .AddResource(serviceBus)
            .WithParentRelationship(builder);
    }

    /// <summary>
    /// Allocates two distinct free TCP ports, holding both listeners open until each port is
    /// read so the second allocation cannot return the first port.
    /// </summary>
    private static (int First, int Second) GetFreeTcpPorts()
    {
        TcpListener first = new(IPAddress.Loopback, 0);
        TcpListener second = new(IPAddress.Loopback, 0);
        try
        {
            first.Start();
            second.Start();
            return (((IPEndPoint)first.LocalEndpoint).Port, ((IPEndPoint)second.LocalEndpoint).Port);
        }
        finally
        {
            first.Stop();
            second.Stop();
        }
    }

    /// <summary>
    /// Mounts the Docker socket into the Floci Azure container so that Azure Functions and other
    /// container-backed services can launch sibling containers.
    /// Also sets <c>FLOCI_AZ_DOCKER_DOCKER_HOST</c> to <c>unix:///var/run/docker.sock</c> (the
    /// container-side path where the socket is always mounted) so Floci can connect to it.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="socketPath">Optional. Host path to the Docker socket (default: <c>/var/run/docker.sock</c>).
    /// Non-standard paths (e.g. Podman at <c>/run/user/1000/podman/podman.sock</c>) are bind-mounted
    /// to <c>/var/run/docker.sock</c> inside the container.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport("withDockerSocketAzure", MethodName = "withDockerSocket")]
    public static IResourceBuilder<FlociAzureContainerResource> WithDockerSocket(
        this IResourceBuilder<FlociAzureContainerResource> builder,
        string socketPath = "/var/run/docker.sock")
        => WithDockerSocketCore(builder, socketPath);

    /// <summary>
    /// Configures a named data volume for persistent Floci Azure state.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="name">The name of the volume to mount.</param>
    /// <param name="isReadOnly">Whether the volume should be read-only.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport("withDataVolumeAzure", MethodName = "withDataVolume")]
    public static IResourceBuilder<FlociAzureContainerResource> WithDataVolume(
        this IResourceBuilder<FlociAzureContainerResource> builder,
        string name,
        bool isReadOnly = false)
        => WithDataVolumeCore(builder, name, isReadOnly);

    /// <summary>
    /// Configures a bind mount for persistent Floci Azure state.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="source">The host path to bind into the container.</param>
    /// <param name="isReadOnly">Whether the bind mount should be read-only.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport("withDataBindMountAzure", MethodName = "withDataBindMount")]
    public static IResourceBuilder<FlociAzureContainerResource> WithDataBindMount(
        this IResourceBuilder<FlociAzureContainerResource> builder,
        string source,
        bool isReadOnly = false)
        => WithDataBindMountCore(builder, source, isReadOnly);

    /// <summary>
    /// Adds a <a href="https://github.com/floci-io/floci-ui">Floci UI</a> web console container
    /// for browsing the resources hosted by the Floci Azure emulator.
    /// </summary>
    /// <ats-summary>Adds a Floci UI web console container for the Floci Azure resource</ats-summary>
    /// <param name="builder">The Floci Azure resource builder.</param>
    /// <param name="configureContainer">Configuration callback for the Floci UI container resource.</param>
    /// <param name="containerName">Optional. The name of the Floci UI container (default: <c>{floci-name}-ui</c>).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociAzureContainerResource}"/> for further resource configuration.</returns>
    [AspireExport("withFlociUIAzure", MethodName = "withFlociUI", RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<FlociAzureContainerResource> WithFlociUI(
        this IResourceBuilder<FlociAzureContainerResource> builder,
        Action<IResourceBuilder<FlociUIContainerResource>>? configureContainer = null,
        string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddOrConfigureFlociUI(builder, configureContainer, containerName);
        return builder;
    }
}

