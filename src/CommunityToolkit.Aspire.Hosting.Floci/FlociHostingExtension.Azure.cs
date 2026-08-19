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

