using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Jellyfin;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Jellyfin to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class JellyfinHostingExtension
{
    /// <summary>
    /// Adds a Jellyfin container resource to the <see cref="IDistributedApplicationBuilder"/>.
    /// </summary>
    /// <remarks>
    /// The container lifetime defaults to <see cref="ContainerLifetime.Persistent"/> because Jellyfin
    /// stores its library database, users, and watch history under <c>/config</c>. Call
    /// <c>.WithLifetime(ContainerLifetime.Session)</c> to opt out.
    /// </remarks>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the Jellyfin resource will be added.</param>
    /// <param name="name">The name of the Jellyfin container resource.</param>
    /// <param name="httpPort">Optional. The host HTTP port on which Jellyfin will be exposed. When <c>null</c>, Aspire assigns a port.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{JellyfinContainerResource}"/> for further resource configuration.</returns>
    [AspireExport("addJellyfin", Description = "Adds a Jellyfin media server container resource")]
    public static IResourceBuilder<JellyfinContainerResource> AddJellyfin(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        JellyfinContainerResource resource = new(name);

        return builder.AddResource(resource)
            .WithImage(JellyfinContainerImageTags.Image)
            .WithImageTag(JellyfinContainerImageTags.Tag)
            .WithImageRegistry(JellyfinContainerImageTags.Registry)
            .WithLifetime(ContainerLifetime.Persistent)
            .WithHttpEndpoint(
                targetPort: JellyfinContainerResource.HttpEndpointPort,
                port: httpPort,
                name: JellyfinContainerResource.HttpEndpointName)
            .WithHttpHealthCheck(
                path: "/health",
                statusCode: 200,
                endpointName: JellyfinContainerResource.HttpEndpointName);
    }

    /// <summary>
    /// Configures a named data volume for the Jellyfin <c>/config</c> directory (library DB, users, settings, plugins).
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/> used to configure the resource.</param>
    /// <param name="name">Optional name of the volume. When <c>null</c>, a default name is derived from the resource name.</param>
    /// <param name="isReadOnly">Whether the volume should be mounted read-only. Defaults to <c>false</c>.</param>
    /// <returns>The same <see cref="IResourceBuilder{T}"/> for chaining.</returns>
    [AspireExport("withDataVolume", Description = "Adds a named volume for the Jellyfin /config folder")]
    public static IResourceBuilder<JellyfinContainerResource> WithDataVolume(
        this IResourceBuilder<JellyfinContainerResource> builder,
        string? name = null,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), JellyfinContainerResource.ConfigTarget, isReadOnly);
    }

    /// <summary>
    /// Configures a bind mount for the Jellyfin <c>/config</c> directory (library DB, users, settings, plugins).
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{JellyfinContainerResource}"/> to configure.</param>
    /// <param name="source">The host path to bind to <c>/config</c>.</param>
    /// <param name="isReadOnly">Whether the bind mount should be read-only. Defaults to <c>false</c>.</param>
    /// <returns>The same <see cref="IResourceBuilder{JellyfinContainerResource}"/> for chaining.</returns>
    [AspireExport("withDataBindMount", Description = "Adds a bind mount for the Jellyfin /config folder")]
    public static IResourceBuilder<JellyfinContainerResource> WithDataBindMount(
        this IResourceBuilder<JellyfinContainerResource> builder,
        string source,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return builder.WithBindMount(source, JellyfinContainerResource.ConfigTarget, isReadOnly);
    }

    /// <summary>
    /// Configures a named cache volume for the Jellyfin <c>/cache</c> directory (transcoding cache).
    /// </summary>
    [AspireExport("withCacheVolume", Description = "Adds a named volume for the Jellyfin /cache folder")]
    public static IResourceBuilder<JellyfinContainerResource> WithCacheVolume(
        this IResourceBuilder<JellyfinContainerResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "cache"), JellyfinContainerResource.CacheTarget, isReadOnly: false);
    }

    /// <summary>
    /// Configures a bind mount for the Jellyfin <c>/cache</c> directory (transcoding cache).
    /// </summary>
    [AspireExport("withCacheBindMount", Description = "Adds a bind mount for the Jellyfin /cache folder")]
    public static IResourceBuilder<JellyfinContainerResource> WithCacheBindMount(
        this IResourceBuilder<JellyfinContainerResource> builder,
        string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return builder.WithBindMount(source, JellyfinContainerResource.CacheTarget, isReadOnly: false);
    }

    /// <summary>
    /// Adds a bind mount for a Jellyfin media library. May be called multiple times to mount multiple libraries
    /// at different container paths (for example <c>/media</c>, <c>/media2</c>, <c>/tv</c>, <c>/movies</c>).
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{JellyfinContainerResource}"/> to configure.</param>
    /// <param name="source">The host path containing the media files.</param>
    /// <param name="target">The container path to mount the media at. Defaults to <c>/media</c>.</param>
    /// <param name="isReadOnly">Whether the bind mount should be read-only. Defaults to <c>true</c> — Jellyfin only needs to read media files.</param>
    /// <returns>The same <see cref="IResourceBuilder{JellyfinContainerResource}"/> for chaining.</returns>
    [AspireExport("withMediaBindMount", Description = "Adds a bind mount for a Jellyfin media library")]
    public static IResourceBuilder<JellyfinContainerResource> WithMediaBindMount(
        this IResourceBuilder<JellyfinContainerResource> builder,
        string source,
        string target = JellyfinContainerResource.DefaultMediaTarget,
        bool isReadOnly = true)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        return builder.WithBindMount(source, target, isReadOnly);
    }

    /// <summary>
    /// Adds a read-only bind mount for additional fonts used when burning in subtitles during transcoding.
    /// Mounts the source directory at <c>/usr/local/share/fonts/custom</c>.
    /// </summary>
    [AspireExport("withFontsBindMount", Description = "Adds a read-only bind mount for custom subtitle-burn-in fonts")]
    public static IResourceBuilder<JellyfinContainerResource> WithFontsBindMount(
        this IResourceBuilder<JellyfinContainerResource> builder,
        string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return builder.WithBindMount(source, JellyfinContainerResource.FontsTarget, isReadOnly: true);
    }

    /// <summary>
    /// Sets the <c>JELLYFIN_PublishedServerUrl</c> environment variable so Jellyfin clients can auto-discover
    /// the server at an external address.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{JellyfinContainerResource}"/> to configure.</param>
    /// <param name="url">The externally reachable URL (for example <c>http://media.example.com</c>).</param>
    /// <returns>The same <see cref="IResourceBuilder{JellyfinContainerResource}"/> for chaining.</returns>
    [AspireExport("withPublishedServerUrl", Description = "Sets the JELLYFIN_PublishedServerUrl env var used for client autodiscovery")]
    public static IResourceBuilder<JellyfinContainerResource> WithPublishedServerUrl(
        this IResourceBuilder<JellyfinContainerResource> builder,
        string url)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        return builder.WithEnvironment(JellyfinContainerResource.PublishedServerUrlEnvVar, url);
    }

    /// <summary>
    /// Exposes Jellyfin's UDP client-autodiscovery endpoint on port 7359.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{JellyfinContainerResource}"/> to configure.</param>
    /// <param name="port">Optional host UDP port. When <c>null</c>, Aspire assigns a port.</param>
    /// <returns>The same <see cref="IResourceBuilder{JellyfinContainerResource}"/> for chaining.</returns>
    [AspireExport("withDiscoveryEndpoint", Description = "Exposes the Jellyfin UDP client-autodiscovery endpoint (port 7359)")]
    public static IResourceBuilder<JellyfinContainerResource> WithDiscoveryEndpoint(
        this IResourceBuilder<JellyfinContainerResource> builder,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.Annotations.Add(new EndpointAnnotation(
            protocol: ProtocolType.Udp,
            name: JellyfinContainerResource.DiscoveryEndpointName,
            port: port,
            targetPort: JellyfinContainerResource.DiscoveryEndpointPort));

        return builder;
    }

    /// <summary>
    /// Exposes Jellyfin's UDP DLNA endpoint on port 1900.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{JellyfinContainerResource}"/> to configure.</param>
    /// <param name="port">Optional host UDP port. When <c>null</c>, Aspire assigns a port.</param>
    /// <returns>The same <see cref="IResourceBuilder{JellyfinContainerResource}"/> for chaining.</returns>
    [AspireExport("withDlnaEndpoint", Description = "Exposes the Jellyfin UDP DLNA endpoint (port 1900)")]
    public static IResourceBuilder<JellyfinContainerResource> WithDlnaEndpoint(
        this IResourceBuilder<JellyfinContainerResource> builder,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.Annotations.Add(new EndpointAnnotation(
            protocol: ProtocolType.Udp,
            name: JellyfinContainerResource.DlnaEndpointName,
            port: port,
            targetPort: JellyfinContainerResource.DlnaEndpointPort));

        return builder;
    }
}

#pragma warning restore ASPIREATS001
