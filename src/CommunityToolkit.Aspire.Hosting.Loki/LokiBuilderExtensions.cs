using Aspire.Hosting.ApplicationModel;

#pragma warning disable ASPIREATS001

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Grafana Loki resources.
/// </summary>
public static class LokiBuilderExtensions
{
    /// <summary>
    /// Adds a Grafana Loki container to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name and connection string name.</param>
    /// <param name="configureSettings">An optional callback that configures the Loki settings.</param>
    /// <returns>The Loki resource builder.</returns>
    [AspireExport(RunSyncOnBackgroundThread = true)]
    public static IResourceBuilder<LokiResource> AddLoki(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        Action<LokiSettings>? configureSettings = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        LokiSettings settings = new();
        configureSettings?.Invoke(settings);
        ArgumentException.ThrowIfNullOrEmpty(settings.LokiTag);
        ArgumentException.ThrowIfNullOrEmpty(settings.Registry);
        ArgumentException.ThrowIfNullOrEmpty(settings.Image);

        var resourceBuilder = builder.AddResource(new LokiResource(name))
            .WithImage(settings.Image, settings.LokiTag)
            .WithImageRegistry(settings.Registry)
            .WithHttpEndpoint(port: settings.HttpPort, targetPort: 3100, name: LokiResource.HttpEndpointName)
            .WithIconName("TextBulletListSquare");

        if (settings.EnableGrpcEndpoint)
        {
            resourceBuilder.WithEndpoint(port: settings.GrpcPort, targetPort: 9096, name: LokiResource.GrpcEndpointName, scheme: "tcp");
        }

        if (!settings.DisableHealthcheck)
        {
            resourceBuilder.WithHttpHealthCheck("/ready", endpointName: LokiResource.HttpEndpointName);
        }

        return resourceBuilder;
    }

    /// <summary>
    /// Adds a named volume for the default Loki configuration's data directory, <c>/tmp/loki</c>.
    /// </summary>
    /// <param name="builder">The Loki resource builder.</param>
    /// <param name="name">The optional volume name. A name is generated when omitted.</param>
    /// <returns>The Loki resource builder.</returns>
    [AspireExport]
    public static IResourceBuilder<LokiResource> WithDataVolume(this IResourceBuilder<LokiResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/tmp/loki");
    }

    /// <summary>
    /// Adds a writable bind mount for the default Loki configuration's data directory, <c>/tmp/loki</c>.
    /// </summary>
    /// <param name="builder">The Loki resource builder.</param>
    /// <param name="source">The host directory. Relative paths are resolved against the AppHost directory.</param>
    /// <returns>The Loki resource builder.</returns>
    /// <remarks>The directory must be writable by the container user (UID 10001).</remarks>
    [AspireExport]
    public static IResourceBuilder<LokiResource> WithDataBindMount(this IResourceBuilder<LokiResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, "/tmp/loki");
    }

    /// <summary>
    /// Mounts a Loki configuration file read-only at <c>/etc/loki/local-config.yaml</c>.
    /// </summary>
    /// <param name="builder">The Loki resource builder.</param>
    /// <param name="source">The host configuration file. Relative paths are resolved against the AppHost directory.</param>
    /// <returns>The Loki resource builder.</returns>
    /// <remarks>Use <c>/tmp/loki</c> for storage paths when combining this method with the data mount helpers.</remarks>
    [AspireExport]
    public static IResourceBuilder<LokiResource> WithConfigBindMount(this IResourceBuilder<LokiResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, "/etc/loki/local-config.yaml", isReadOnly: true)
            .WithArgs("-config.file=/etc/loki/local-config.yaml");
    }
}

#pragma warning restore ASPIREATS001