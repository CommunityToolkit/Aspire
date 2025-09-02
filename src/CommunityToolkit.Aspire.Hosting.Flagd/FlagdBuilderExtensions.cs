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
    /// <param name="fileSource">The path to the flag configuration file on the host. The flags configuration should be stored in a file named flagd.json</param>
    /// <param name="port">The host port for flagd HTTP endpoint. If not provided, a random port will be assigned.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> AddFlagd(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string fileSource,
        int port = 8013)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));
        ArgumentException.ThrowIfNullOrEmpty(fileSource, nameof(fileSource));
        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1, nameof(port));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535, nameof(port));

        var resource = new FlagdResource(name);

        return builder.AddResource(resource)
            .WithImage(FlagdContainerImageTags.Image, FlagdContainerImageTags.Tag)
            .WithImageRegistry(FlagdContainerImageTags.Registry)
            .WithHttpEndpoint(port: 8013, targetPort: port, name: FlagdResource.HttpEndpointName)
            .WithBindMount(fileSource, "/flags")
            .WithArgs("start", "--uri", "file:./flags/flagd.json");
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
