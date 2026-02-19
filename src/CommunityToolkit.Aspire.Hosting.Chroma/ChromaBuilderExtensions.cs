using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Chroma;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding ChromaDB resources to the application model.
/// </summary>
public static class ChromaBuilderExtensions
{
    private const int ChromaPort = 8000;

    /// <summary>
    /// Adds a ChromaDB container resource to the application model.
    /// The default image is <inheritdoc cref="ChromaContainerImageTags.Image"/> and the tag is <inheritdoc cref="ChromaContainerImageTags.Tag"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ChromaResource> AddChroma(
        this IDistributedApplicationBuilder builder,
        string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var chroma = new ChromaResource(name);

        return builder.AddResource(chroma)
             .WithImage(ChromaContainerImageTags.Image, ChromaContainerImageTags.Tag)
             .WithImageRegistry(ChromaContainerImageTags.Registry)
             .WithHttpEndpoint(targetPort: ChromaPort, port: port, name: ChromaResource.PrimaryEndpointName);
    }

    /// <summary>
    /// Adds a named volume for the data folder to a ChromaDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ChromaResource> WithDataVolume(this IResourceBuilder<ChromaResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/chroma/chroma");
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a ChromaDB container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ChromaResource> WithDataBindMount(this IResourceBuilder<ChromaResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/chroma/chroma");
    }
}
