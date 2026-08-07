using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci;

namespace Aspire.Hosting;

public static partial class FlociHostingExtension
{
    /// <summary>
    /// Adds a Floci GCP emulator container resource to the <see cref="IDistributedApplicationBuilder"/>.
    /// </summary>
    /// <ats-summary>Adds a Floci GCP emulator container resource</ats-summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the Floci resource will be added.</param>
    /// <param name="name">The name of the Floci container resource.</param>
    /// <param name="port">Optional. The host port to bind for the GCP endpoint.</param>
    /// <param name="defaultProjectId">Optional. The default GCP project ID (default: <c>floci-local</c>).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociGcpContainerResource}"/> for further resource configuration.</returns>
    [AspireExport]
    public static IResourceBuilder<FlociGcpContainerResource> AddFlociGcp(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null,
        string defaultProjectId = "floci-local")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        FlociGcpContainerResource resource = new(name) { DefaultProjectId = defaultProjectId };

        var flociBuilder = builder.AddResource(resource)
            .WithImage(FlociContainerImageTags.GcpImage)
            .WithImageTag(FlociContainerImageTags.GcpTag)
            .WithImageRegistry(FlociContainerImageTags.GcpRegistry)
            .WithHttpEndpoint(
                targetPort: FlociGcpContainerResource.EndpointPort,
                port: port,
                name: resource.EndpointName)
            .WithEnvironment(FlociGcpContainerResource.HostnameEnvVar, name)
            .WithEnvironment(FlociGcpContainerResource.DefaultProjectIdEnvVar, defaultProjectId)
            .WithEnvironment(resource.StorageModeEnvVar, "memory")
            .WithHttpHealthCheck(
                path: "/_floci-gcp/health",
                statusCode: 200,
                endpointName: resource.EndpointName);

        return flociBuilder;
    }

    /// <summary>
    /// Adds a reference to a Floci GCP emulator resource, injecting the standard
    /// <c>ConnectionStrings__{name}</c> entry plus the <c>*_EMULATOR_HOST</c> environment variables
    /// the Google Cloud SDKs already honor, along with <c>GOOGLE_CLOUD_PROJECT</c> and
    /// <c>CLOUDSDK_CORE_PROJECT</c>.
    /// </summary>
    /// <ats-summary>Adds a reference to a Floci GCP emulator resource</ats-summary>
    /// <typeparam name="TDestination">The type of the resource receiving the reference.</typeparam>
    /// <param name="builder">The resource builder for the resource receiving the reference.</param>
    /// <param name="floci">The Floci GCP resource to reference.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{TDestination}"/> for further configuration.</returns>
    [AspireExport("withFlociGcpReference")]
    public static IResourceBuilder<TDestination> WithReference<TDestination>(
        this IResourceBuilder<TDestination> builder,
        IResourceBuilder<FlociGcpContainerResource> floci)
        where TDestination : IResourceWithEnvironment
        => WithFlociReferenceCore(builder, floci, static (context, resource, endpoint) =>
        {
            // The Google SDKs expect host:port for these, but STORAGE_EMULATOR_HOST is a full URL.
            context.EnvironmentVariables["PUBSUB_EMULATOR_HOST"] = endpoint.HostAndPort;
            context.EnvironmentVariables["FIRESTORE_EMULATOR_HOST"] = endpoint.HostAndPort;
            context.EnvironmentVariables["DATASTORE_EMULATOR_HOST"] = endpoint.HostAndPort;
            context.EnvironmentVariables["STORAGE_EMULATOR_HOST"] = endpoint.Url;
            context.EnvironmentVariables["SECRET_MANAGER_EMULATOR_HOST"] = endpoint.HostAndPort;
            context.EnvironmentVariables["GOOGLE_CLOUD_PROJECT"] = resource.DefaultProjectId;
            context.EnvironmentVariables["CLOUDSDK_CORE_PROJECT"] = resource.DefaultProjectId;
        });

    /// <summary>
    /// Mounts the Docker socket into the Floci GCP container so that Cloud Run, Cloud SQL, and other
    /// container-backed services can launch sibling containers.
    /// Also sets <c>FLOCI_GCP_DOCKER_DOCKER_HOST</c> to <c>unix:///var/run/docker.sock</c> (the
    /// container-side path where the socket is always mounted) so Floci can connect to it.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="socketPath">Optional. Host path to the Docker socket (default: <c>/var/run/docker.sock</c>).
    /// Non-standard paths (e.g. Podman at <c>/run/user/1000/podman/podman.sock</c>) are bind-mounted
    /// to <c>/var/run/docker.sock</c> inside the container.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport("withDockerSocketGcp", MethodName = "withDockerSocket")]
    public static IResourceBuilder<FlociGcpContainerResource> WithDockerSocket(
        this IResourceBuilder<FlociGcpContainerResource> builder,
        string socketPath = "/var/run/docker.sock")
        => WithDockerSocketCore(builder, socketPath);

    /// <summary>
    /// Configures a named data volume for persistent Floci GCP state.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="name">The name of the volume to mount.</param>
    /// <param name="isReadOnly">Whether the volume should be read-only.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport("withDataVolumeGcp", MethodName = "withDataVolume")]
    public static IResourceBuilder<FlociGcpContainerResource> WithDataVolume(
        this IResourceBuilder<FlociGcpContainerResource> builder,
        string name,
        bool isReadOnly = false)
        => WithDataVolumeCore(builder, name, isReadOnly);

    /// <summary>
    /// Configures a bind mount for persistent Floci GCP state.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="source">The host path to bind into the container.</param>
    /// <param name="isReadOnly">Whether the bind mount should be read-only.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport("withDataBindMountGcp", MethodName = "withDataBindMount")]
    public static IResourceBuilder<FlociGcpContainerResource> WithDataBindMount(
        this IResourceBuilder<FlociGcpContainerResource> builder,
        string source,
        bool isReadOnly = false)
        => WithDataBindMountCore(builder, source, isReadOnly);

    /// <summary>
    /// Adds a <a href="https://github.com/floci-io/floci-ui">Floci UI</a> web console container
    /// for browsing the resources hosted by the Floci GCP emulator.
    /// </summary>
    /// <ats-summary>Adds a Floci UI web console container for the Floci GCP resource</ats-summary>
    /// <param name="builder">The Floci GCP resource builder.</param>
    /// <param name="configureContainer">Configuration callback for the Floci UI container resource.</param>
    /// <param name="containerName">Optional. The name of the Floci UI container (default: <c>{floci-name}-ui</c>).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociGcpContainerResource}"/> for further resource configuration.</returns>
    [AspireExport("withFlociUIGcp", MethodName = "withFlociUI", RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<FlociGcpContainerResource> WithFlociUI(
        this IResourceBuilder<FlociGcpContainerResource> builder,
        Action<IResourceBuilder<FlociUIContainerResource>>? configureContainer = null,
        string? containerName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        AddOrConfigureFlociUI(builder, configureContainer, containerName);
        return builder;
    }
}

