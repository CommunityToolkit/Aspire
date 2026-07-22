using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

public static partial class FlociHostingExtension
{
    private const string ContainerSocketPath = "/var/run/docker.sock";

    /// <summary>
    /// Shared implementation behind every provider's <c>WithDockerSocket</c> overload: mounts the
    /// Docker socket and points the resource's <see cref="FlociContainerResource.DockerHostEnvVar"/>
    /// at it so container-backed services (Lambda, Azure Functions, Cloud Run, ...) can launch
    /// sibling containers.
    /// </summary>
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

#pragma warning restore ASPIREATS001
