using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

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

        // BeforeStartEvent: inject the *_EMULATOR_HOST vars the GCP SDKs already honor into every
        // resource that called WithReference(floci). Deferred so resources can be wired up in any
        // order in Program.cs without worrying about whether Floci is fully configured yet.
        builder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            var appModel = evt.Services.GetRequiredService<DistributedApplicationModel>();

            foreach (var dependent in appModel.Resources)
            {
                bool referencesFloci = dependent.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Any(a => ReferenceEquals(a.Resource, resource));

                if (!referencesFloci)
                    continue;

                if (dependent is ContainerResource)
                {
                    // Containers cannot reach the host via localhost — use host.docker.internal
                    // so they can reach the host-exposed Floci port (4588).
                    var flociPort = resource.Port;
                    dependent.Annotations.Add(
                        new ContainerRuntimeArgsCallbackAnnotation(
                            args => args.Add("--add-host=host.docker.internal:host-gateway")));
                    dependent.Annotations.Add(new EnvironmentCallbackAnnotation(ctx =>
                    {
                        var hostAndPort = ReferenceExpression.Create($"host.docker.internal:{flociPort}");
                        ctx.EnvironmentVariables["PUBSUB_EMULATOR_HOST"] = hostAndPort;
                        ctx.EnvironmentVariables["FIRESTORE_EMULATOR_HOST"] = hostAndPort;
                        ctx.EnvironmentVariables["DATASTORE_EMULATOR_HOST"] = hostAndPort;
                        ctx.EnvironmentVariables["STORAGE_EMULATOR_HOST"] = ReferenceExpression.Create($"http://{hostAndPort}");
                        ctx.EnvironmentVariables["SECRET_MANAGER_EMULATOR_HOST"] = hostAndPort;
                        ctx.EnvironmentVariables["GOOGLE_CLOUD_PROJECT"] = resource.DefaultProjectId;
                        ctx.EnvironmentVariables["CLOUDSDK_CORE_PROJECT"] = resource.DefaultProjectId;
                    }));
                }
                else
                {
                    // Host processes (projects, executables) use the standard connection string
                    // which resolves to http://localhost:{port}.
                    dependent.Annotations.Add(new EnvironmentCallbackAnnotation(ctx =>
                    {
                        var hostAndPort = ReferenceExpression.Create($"{resource.Host}:{resource.Port}");
                        ctx.EnvironmentVariables["PUBSUB_EMULATOR_HOST"] = hostAndPort;
                        ctx.EnvironmentVariables["FIRESTORE_EMULATOR_HOST"] = hostAndPort;
                        ctx.EnvironmentVariables["DATASTORE_EMULATOR_HOST"] = hostAndPort;
                        ctx.EnvironmentVariables["STORAGE_EMULATOR_HOST"] = resource.ConnectionStringExpression;
                        ctx.EnvironmentVariables["SECRET_MANAGER_EMULATOR_HOST"] = hostAndPort;
                        ctx.EnvironmentVariables["GOOGLE_CLOUD_PROJECT"] = resource.DefaultProjectId;
                        ctx.EnvironmentVariables["CLOUDSDK_CORE_PROJECT"] = resource.DefaultProjectId;
                    }));
                }
            }

            return Task.CompletedTask;
        });

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

#pragma warning restore ASPIREATS001
