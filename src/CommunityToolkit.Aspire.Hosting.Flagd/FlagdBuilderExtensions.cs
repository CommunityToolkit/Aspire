using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Flagd;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding flagd resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class FlagdBuilderExtensions
{
    /// <summary>
    /// Adds a flagd container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port for flagd HTTP/gRPC endpoints. If not provided, a random port will be assigned.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> AddFlagd(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        var resource = new FlagdResource(name);
        
        return builder.AddResource(resource)
            .WithImage(FlagdContainerImageTags.Image, FlagdContainerImageTags.Tag)
            .WithImageRegistry(FlagdContainerImageTags.Registry)
            .WithHttpEndpoint(port: port, targetPort: 8013, name: FlagdResource.HttpEndpointName)
            .WithEndpoint(port: port, targetPort: 8013, name: FlagdResource.GrpcEndpointName, scheme: "grpc")
            .WithArgs("start");
    }

    /// <summary>
    /// Adds a flag source URI to the flagd resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="uri">The URI of the flag source (e.g., file:///etc/flagd/flags.json, http://example.com/flags.json).</param>
    /// <returns>The <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> WithFlagSource(
        this IResourceBuilder<FlagdResource> builder,
        string uri)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(uri, nameof(uri));

        builder.Resource.AddFlagSource(uri);
        return builder.WithArgs("--uri", uri);
    }

    /// <summary>
    /// Adds a flag configuration file from the local filesystem to the flagd resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The path to the flag configuration file on the host.</param>
    /// <param name="target">The path where the file should be mounted in the container. Defaults to /etc/flagd/flags.json.</param>
    /// <returns>The <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> WithFlagConfigurationFile(
        this IResourceBuilder<FlagdResource> builder,
        string source,
        string target = "/etc/flagd/flags.json")
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(source, nameof(source));
        ArgumentException.ThrowIfNullOrEmpty(target, nameof(target));

        var flagSourceUri = $"file://{target}";
        builder.Resource.AddFlagSource(flagSourceUri);
        
        return builder
            .WithBindMount(source, target)
            .WithArgs("--uri", flagSourceUri);
    }

    /// <summary>
    /// Configures flagd to use HTTP sync provider instead of file-based sync.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="httpUrl">The HTTP URL to fetch flag configurations from.</param>
    /// <param name="interval">The sync interval in seconds. Defaults to 5 seconds.</param>
    /// <returns>The <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> WithHttpSync(
        this IResourceBuilder<FlagdResource> builder,
        string httpUrl,
        int interval = 5)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(httpUrl, nameof(httpUrl));

        builder.Resource.AddFlagSource(httpUrl);
        
        return builder
            .WithArgs("--uri", httpUrl)
            .WithEnvironment("FLAGD_SYNC_PROVIDER_INTERVAL", interval.ToString());
    }

    /// <summary>
    /// Adds a data volume to the flagd container for persistent flag storage.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. If not provided, a name will be generated.</param>
    /// <param name="isReadOnly">Whether the volume should be mounted as read-only.</param>
    /// <returns>The <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> WithDataVolume(
        this IResourceBuilder<FlagdResource> builder,
        string? name = null,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        var volumeName = name ?? $"{builder.Resource.Name}-data";
        return builder.WithVolume(volumeName, "/etc/flagd", isReadOnly);
    }

    /// <summary>
    /// Configures logging level for flagd.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="level">The logging level (debug, info, warn, error).</param>
    /// <returns>The <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> WithLogging(
        this IResourceBuilder<FlagdResource> builder,
        string level = "info")
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(level, nameof(level));

        return builder.WithEnvironment("FLAGD_LOG_LEVEL", level.ToUpperInvariant());
    }
}
