namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding a HomeAssistant container to the application model.
/// </summary>
public static class HomeAssistantResourceBuilderExtensions
{
    /// <summary>
    /// Adds a <see cref="HomeAssistantResource"/> to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the <see cref="HomeAssistantResource"/>.</param>
    /// <param name="port">The optional port for the HTTP endpoint.</param>
    /// <remarks>
    /// This version of the package defaults to <inheritdoc cref="HomeAssistantContainerImageTags.Registry"/>/<inheritdoc cref="HomeAssistantContainerImageTags.Image"/>:<inheritdoc cref="HomeAssistantContainerImageTags.Tag"/> container image.
    /// </remarks>
    /// <returns>The source builder for the <see cref="HomeAssistantResource"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="builder"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    public static IResourceBuilder<HomeAssistantResource> AddHomeAssistant(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var resource = new HomeAssistantResource(name);

        return builder.AddResource(resource)
             .WithAnnotation(new ContainerImageAnnotation
             {
                 Image = HomeAssistantContainerImageTags.Image,
                 Tag = HomeAssistantContainerImageTags.Tag,
                 Registry = HomeAssistantContainerImageTags.Registry
             })
             .WithHttpEndpoint(port: port, targetPort: 8123, name: HomeAssistantResource.HomeAssistantEndpointName)
             .WithOtlpExporter()
             .WithHttpHealthCheck("/")
             .ExcludeFromManifest();
    }

    /// <summary>
    /// Adds a data volume to the HomeAssistant container at the <i>/config</i> path.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and source names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<HomeAssistantResource> WithDataVolume(
        this IResourceBuilder<HomeAssistantResource> builder,
        string? name = null,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

#pragma warning disable CTASPIRE001
        return builder.WithVolume(name ?? VolumeNameGenerator.CreateVolumeName(builder, "home-assistant"), "/config", isReadOnly);
#pragma warning restore CTASPIRE001
    }

    /// <summary>
    /// Adds a data mount to the HomeAssistant container at the <i>/config</i> path. The given <paramref name="source"/> can include
    /// a <i>configuration.yaml</i> file. For more information, see <a href="https://www.home-assistant.io/docs/configuration"></a>.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="source">The source path that contains the <i>configuration.yaml</i>.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<HomeAssistantResource> WithDataBindMount(
        this IResourceBuilder<HomeAssistantResource> builder,
        string source,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var appHostDirectory = builder.ApplicationBuilder.AppHostDirectory;

        var normalizedPath = PathNormalizer.NormalizePathForCurrentPlatform(Path.Combine(appHostDirectory, source));

        if (Directory.Exists(normalizedPath) is false)
        {
            throw new FileNotFoundException($"The provided '{normalizedPath}' directory doesn't exist.", source);
        }

        return builder.WithBindMount(normalizedPath, target: "/config", isReadOnly);
    }
}
