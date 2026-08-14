using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Floci;

namespace Aspire.Hosting;

public static partial class FlociHostingExtension
{
    private const string ContainerSocketPath = "/var/run/docker.sock";

    /// <summary>
    /// Shared plumbing behind every provider's <c>WithReference</c> overload: adds the standard
    /// <c>ConnectionStrings__{name}</c> entry, then hands the caller the Floci endpoint as
    /// <see cref="ReferenceExpression"/>s so it only has to name its provider-specific environment
    /// variables.
    /// </summary>
    /// <remarks>
    /// The expressions are deliberately left unresolved: Aspire performs context-based endpoint
    /// resolution when it materialises the dependent's environment, so a project gets
    /// <c>localhost:{hostPort}</c>, a sibling container gets <c>{flociName}:{targetPort}</c> on the
    /// container network, and neither depends on a hard-coded <c>host.docker.internal</c> mapping
    /// that only some container runtimes provide.
    /// </remarks>
    internal static IResourceBuilder<TDestination> WithFlociReferenceCore<TDestination, TFloci>(
        IResourceBuilder<TDestination> builder,
        IResourceBuilder<TFloci> floci,
        Action<EnvironmentCallbackContext, TFloci, FlociEndpoint> configureEnvironment)
        where TDestination : IResourceWithEnvironment
        where TFloci : FlociContainerResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(floci);

        TFloci resource = floci.Resource;

        // Typed as the base interface so overload resolution binds to Aspire's built-in
        // WithReference (which injects ConnectionStrings__{name} and its connection properties)
        // rather than recursing back into the provider-specific overload that called us.
        IResourceBuilder<IResourceWithConnectionString> connectionStringSource = floci;
        builder.WithReference(connectionStringSource);

        // Built inside the callback, not captured here, so the scheme is read after the whole
        // AppHost has been configured, so a certificate configured after WithReference still applies.
        return builder.WithEnvironment(context =>
        {
            FlociEndpoint endpoint = new(
                HostAndPort: ReferenceExpression.Create($"{resource.Host}:{resource.Port}"),
                Url: resource.ConnectionStringExpression);

            configureEnvironment(context, resource, endpoint);
        });
    }

    /// <summary>
    /// Shared implementation behind every provider's <c>WithDockerSocket</c> overload: mounts the
    /// Docker socket and points the resource's <see cref="FlociContainerResource.DockerHostEnvVar"/>
    /// at it so container-backed services (Lambda, Azure Functions, Cloud Run, ...) can launch
    /// sibling containers.
    /// </summary>
    /// <remarks>
    /// <see cref="ContainerSocketPath"/> is the path *inside* the Linux container, so it is a Unix
    /// path on every host OS. The default <c>socketPath</c> is also correct on Windows and macOS:
    /// Docker Desktop and Rancher Desktop expose the engine at <c>/var/run/docker.sock</c> for bind
    /// mounts regardless of the host's native transport (named pipe on Windows). Hosts that place
    /// the socket elsewhere — Podman, or a rootless daemon — pass <c>socketPath</c> explicitly.
    /// </remarks>
    internal static IResourceBuilder<TFloci> WithDockerSocketCore<TFloci>(
        IResourceBuilder<TFloci> builder,
        string socketPath)
        where TFloci : FlociContainerResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(socketPath);

        return builder
            .WithEnvironment(builder.Resource.DockerHostEnvVar, $"unix://{ContainerSocketPath}")
            .WithContainerRuntimeArgs("-u", "root", "-v", $"{socketPath}:{ContainerSocketPath}");
    }

    /// <summary>
    /// Shared implementation behind every provider's <c>WithDataVolume</c> overload: switches the
    /// resource to persistent storage mode and mounts a named volume for it.
    /// </summary>
    internal static IResourceBuilder<TFloci> WithDataVolumeCore<TFloci>(
        IResourceBuilder<TFloci> builder,
        string name,
        bool isReadOnly)
        where TFloci : FlociContainerResource
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithEnvironment(builder.Resource.StorageModeEnvVar, "persistent")
            .WithVolume(name, "/app/data", isReadOnly);
    }

    /// <summary>
    /// Shared implementation behind every provider's <c>WithDataBindMount</c> overload: switches
    /// the resource to persistent storage mode and bind-mounts a host path for it.
    /// </summary>
    internal static IResourceBuilder<TFloci> WithDataBindMountCore<TFloci>(
        IResourceBuilder<TFloci> builder,
        string source,
        bool isReadOnly)
        where TFloci : FlociContainerResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder
            .WithEnvironment(builder.Resource.StorageModeEnvVar, "persistent")
            .WithBindMount(source, "/app/data", isReadOnly);
    }
}

