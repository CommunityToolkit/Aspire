using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIREATS001 // AspireExport is experimental
#pragma warning disable ASPIRECERTIFICATES001 // Certificate APIs are experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Floci to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class FlociHostingExtension
{
    /// <summary>
    /// Adds a Floci AWS emulator container resource to the <see cref="IDistributedApplicationBuilder"/>.
    /// </summary>
    /// <ats-summary>Adds a Floci AWS emulator container resource</ats-summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the Floci resource will be added.</param>
    /// <param name="name">The name of the Floci container resource.</param>
    /// <param name="port">Optional. The host port to bind for the AWS endpoint.</param>
    /// <param name="defaultRegion">Optional. The default AWS region (default: <c>us-east-1</c>).</param>
    /// <param name="defaultAccountId">Optional. The default AWS account ID (default: <c>000000000000</c>).</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlociContainerResource}"/> for further resource configuration.</returns>
    [AspireExport]
    public static IResourceBuilder<FlociContainerResource> AddFloci(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null,
        string defaultRegion = "us-east-1",
        string defaultAccountId = "000000000000")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        FlociContainerResource resource = new(name) { DefaultRegion = defaultRegion };

        // BeforeStartEvent: inject standard AWS env vars into every resource that called
        // WithReference(floci). Standard WithReference already injects ConnectionStrings__<name>;
        // this subscriber additionally sets AWS_ENDPOINT_URL (and companions) so the AWS SDK
        // needs no extra configuration in dependent services.
        //
        // Processing is deferred to BeforeStartEvent so resources can be wired up in any order
        // in Program.cs without worrying about whether Floci is fully configured yet.
        // TlsEnabled is read at BeforeStartEvent time (after all extension methods have run).
        builder.Eventing.Subscribe<BeforeStartEvent>((evt, ct) =>
        {
            var appModel = evt.Services.GetRequiredService<DistributedApplicationModel>();

            foreach (var dependent in appModel.Resources)
            {
                // Standard WithReference(floci) adds a ResourceRelationshipAnnotation pointing to
                // our resource. Detect dependents without needing a separate tracking collection.
                bool referencesFloci = dependent.Annotations
                    .OfType<ResourceRelationshipAnnotation>()
                    .Any(a => ReferenceEquals(a.Resource, resource));

                if (!referencesFloci)
                    continue;

                if (dependent is ContainerResource)
                {
                    // Containers cannot reach the host via localhost — use host.docker.internal
                    // so they can reach the host-exposed Floci port.
                    var flociPort = resource.Port;
                    // Ensure host.docker.internal resolves inside containers.
                    dependent.Annotations.Add(
                        new ContainerRuntimeArgsCallbackAnnotation(
                            args => args.Add("--add-host=host.docker.internal:host-gateway")));
                    dependent.Annotations.Add(new EnvironmentCallbackAnnotation(ctx =>
                    {
                        var scheme = resource.TlsEnabled ? "https" : "http";
                        ctx.EnvironmentVariables["AWS_ENDPOINT_URL"] =
                            ReferenceExpression.Create($"{scheme}://host.docker.internal:{flociPort}");
                        ctx.EnvironmentVariables["AWS_DEFAULT_REGION"] = resource.DefaultRegion;
                        ctx.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = "test";
                        ctx.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = "test";
                    }));
                }
                else
                {
                    // Host processes (projects, executables) use the standard connection string
                    // which resolves to http(s)://localhost:{port} and is scheme-aware when TLS is enabled.
                    dependent.Annotations.Add(new EnvironmentCallbackAnnotation(ctx =>
                    {
                        ctx.EnvironmentVariables["AWS_ENDPOINT_URL"] = resource.ConnectionStringExpression;
                        ctx.EnvironmentVariables["AWS_DEFAULT_REGION"] = resource.DefaultRegion;
                        ctx.EnvironmentVariables["AWS_ACCESS_KEY_ID"] = "test";
                        ctx.EnvironmentVariables["AWS_SECRET_ACCESS_KEY"] = "test";
                    }));
                }
            }

            return Task.CompletedTask;
        });

        var flociBuilder = builder.AddResource(resource)
            .WithImage(FlociContainerImageTags.Image)
            .WithImageTag(FlociContainerImageTags.Tag)
            .WithImageRegistry(FlociContainerImageTags.Registry)
            .WithHttpEndpoint(
                targetPort: FlociContainerResource.AwsEndpointPort,
                port: port,
                name: FlociContainerResource.AwsEndpointName)
            .WithEnvironment(FlociContainerResource.HostnameEnvVar, name)
            .WithEnvironment(FlociContainerResource.DefaultRegionEnvVar, defaultRegion)
            .WithEnvironment(FlociContainerResource.DefaultAccountIdEnvVar, defaultAccountId)
            .WithEnvironment(FlociContainerResource.StorageModeEnvVar, "memory")
            .WithHttpHealthCheck(
                path: "/_floci/info",
                statusCode: 200,
                endpointName: FlociContainerResource.AwsEndpointName);

        // Register the TLS env-var wiring once inside AddFloci.
        // WithHttpsCertificateConfiguration fires when a certificate IS configured on this resource
        // (e.g. the caller chains .WithHttpsDeveloperCertificate() or .WithHttpsCertificate(cert))
        // OR when the Aspire developer certificate service indicates HTTPS should be used by default.
        // This makes TLS transparent: no separate WithTls() wrapper is needed.
        flociBuilder
            .WithHttpsCertificateConfiguration(ctx =>
            {
                resource.TlsEnabled = true;
                ctx.EnvironmentVariables[FlociContainerResource.TlsEnabledEnvVar] = "true";
                ctx.EnvironmentVariables[FlociContainerResource.TlsCertPathEnvVar] = ctx.CertificatePath;
                ctx.EnvironmentVariables[FlociContainerResource.TlsKeyPathEnvVar] = ctx.KeyPath;
                return Task.CompletedTask;
            })
            // Make Floci trust the Aspire developer certificate so it can reach other services
            // over HTTPS in local development without certificate errors.
            .WithDeveloperCertificateTrust(true);

        return flociBuilder;
    }

    /// <summary>
    /// Mounts the Docker socket into the Floci container so that Lambda and other
    /// container-backed AWS services can launch sibling containers.
    /// Also sets <c>FLOCI_DOCKER_DOCKER_HOST</c> to <c>unix:///var/run/docker.sock</c> (the
    /// container-side path where the socket is always mounted) so Floci can connect to it.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="socketPath">Optional. Host path to the Docker socket (default: <c>/var/run/docker.sock</c>).
    /// Non-standard paths (e.g. Podman at <c>/run/user/1000/podman/podman.sock</c>) are bind-mounted
    /// to <c>/var/run/docker.sock</c> inside the container.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport]
    public static IResourceBuilder<FlociContainerResource> WithDockerSocket(
        this IResourceBuilder<FlociContainerResource> builder,
        string socketPath = "/var/run/docker.sock")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(socketPath);

        const string containerSocketPath = "/var/run/docker.sock";
        return builder
            .WithEnvironment(FlociContainerResource.DockerHostEnvVar, $"unix://{containerSocketPath}")
            .WithContainerRuntimeArgs("-u", "root", "-v", $"{socketPath}:{containerSocketPath}");
    }

    /// <summary>
    /// Mounts a custom Quarkus <c>application.yml</c> configuration file into the Floci container.
    /// The file is mounted read-only at <c>/deployments/config/application.yml</c>, which Quarkus
    /// reads on startup and merges with built-in defaults.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="hostPath">The host-side path to the <c>application.yml</c> file to mount.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport]
    public static IResourceBuilder<FlociContainerResource> WithConfigFile(
        this IResourceBuilder<FlociContainerResource> builder,
        string hostPath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(hostPath);

        return builder.WithBindMount(hostPath, FlociContainerResource.ConfigMountPath, isReadOnly: true);
    }

    /// <summary>
    /// Configures a named data volume for persistent Floci state.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="name">The name of the volume to mount.</param>
    /// <param name="isReadOnly">Whether the volume should be read-only.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport]
    public static IResourceBuilder<FlociContainerResource> WithDataVolume(
        this IResourceBuilder<FlociContainerResource> builder,
        string name,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithEnvironment(FlociContainerResource.StorageModeEnvVar, "persistent")
            .WithVolume(name, "/app/data", isReadOnly);
    }

    /// <summary>
    /// Configures a bind mount for persistent Floci state.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="source">The host path to bind into the container.</param>
    /// <param name="isReadOnly">Whether the bind mount should be read-only.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/> for further configuration.</returns>
    [AspireExport]
    public static IResourceBuilder<FlociContainerResource> WithDataBindMount(
        this IResourceBuilder<FlociContainerResource> builder,
        string source,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder
            .WithEnvironment(FlociContainerResource.StorageModeEnvVar, "persistent")
            .WithBindMount(source, "/app/data", isReadOnly);
    }

}

#pragma warning restore ASPIREATS001
#pragma warning restore ASPIRECERTIFICATES001
