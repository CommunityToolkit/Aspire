using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

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

        // BeforeStartEvent: inject an Azure Storage connection string into every resource that
        // called WithReference(floci). Deferred so resources can be wired up in any order in
        // Program.cs without worrying about whether Floci is fully configured yet.
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
                    // so they can reach the host-exposed Floci port (4577).
                    var flociPort = resource.Port;
                    dependent.Annotations.Add(
                        new ContainerRuntimeArgsCallbackAnnotation(
                            args => args.Add("--add-host=host.docker.internal:host-gateway")));
                    dependent.Annotations.Add(new EnvironmentCallbackAnnotation(ctx =>
                    {
                        var blobEndpoint = ReferenceExpression.Create($"http://host.docker.internal:{flociPort}/{FlociAzureContainerResource.DefaultAccountName}");
                        ctx.EnvironmentVariables["AZURE_STORAGE_CONNECTION_STRING"] = ReferenceExpression.Create(
                            $"DefaultEndpointsProtocol=http;AccountName={FlociAzureContainerResource.DefaultAccountName};AccountKey={FlociAzureContainerResource.DefaultAccountKey};BlobEndpoint={blobEndpoint};");
                    }));
                }
                else
                {
                    // Host processes (projects, executables) use the standard connection string
                    // which resolves to http://localhost:{port}.
                    dependent.Annotations.Add(new EnvironmentCallbackAnnotation(ctx =>
                    {
                        var blobEndpoint = ReferenceExpression.Create($"{resource.ConnectionStringExpression}/{FlociAzureContainerResource.DefaultAccountName}");
                        ctx.EnvironmentVariables["AZURE_STORAGE_CONNECTION_STRING"] = ReferenceExpression.Create(
                            $"DefaultEndpointsProtocol=http;AccountName={FlociAzureContainerResource.DefaultAccountName};AccountKey={FlociAzureContainerResource.DefaultAccountKey};BlobEndpoint={blobEndpoint};");
                    }));
                }
            }

            return Task.CompletedTask;
        });

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

        return flociBuilder;
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

#pragma warning restore ASPIREATS001
