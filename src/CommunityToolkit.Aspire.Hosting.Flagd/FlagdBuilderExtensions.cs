using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Flagd;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding flagd resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class FlagdBuilderExtensions
{
    private const int FlagdPort = 8013;
    private const int HealthCheckPort = 8014;
    private const int OfrepEndpoint = 8016;

    /// <summary>
    /// Adds a flagd container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port for flagd HTTP endpoint. If not provided, a random port will be assigned.</param>
    /// <param name="ofrepPort">The host port for flagd OFREP endpoint. If not provided, a random port will be assigned.</param>
    /// <remarks>The flagd container requires a sync source to be configured.</remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> AddFlagd(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? port = null,
        int? ofrepPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        var resource = new FlagdResource(name);

        return builder.AddResource(resource)
            .WithImage(FlagdContainerImageTags.Image, FlagdContainerImageTags.Tag)
            .WithImageRegistry(FlagdContainerImageTags.Registry)
            .WithHttpEndpoint(port: port, targetPort: FlagdPort, name: FlagdResource.HttpEndpointName)
            .WithHttpEndpoint(null, HealthCheckPort, FlagdResource.HealthCheckEndpointName)
            .WithHttpHealthCheck("/healthz", endpointName: FlagdResource.HealthCheckEndpointName)
            .WithHttpEndpoint(ofrepPort, OfrepEndpoint, FlagdResource.OfrepEndpointName)
            .WithArgs("start");
    }

    /// <summary>
    /// Configures logging level for flagd. If a flag or targeting rule isn't proceeding the way you'd expect this can be enabled to get more verbose logging.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <returns>The <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> WithLogging(
        this IResourceBuilder<FlagdResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.WithEnvironment("FLAGD_DEBUG", "true");
    }

    /// <summary>
    /// Configures logging level for flagd. If a flag or targeting rule isn't proceeding the way you'd expect this can be enabled to get more verbose logging.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="logLevel">The log level to use. Currently only debug is supported.</param>
    /// <returns>The <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the log level is not valid.</exception>
    /// <remarks>Currently only debug is supported.</remarks>
    public static IResourceBuilder<FlagdResource> WithLoglevel(
        this IResourceBuilder<FlagdResource> builder,
        LogLevel logLevel)
    {
        if (logLevel == LogLevel.Debug)
        {
            return builder.WithEnvironment("FLAGD_DEBUG", "true");
        }

        throw new InvalidOperationException("Only debug log level is supported");
    }

    /// <summary>
    /// Configures flagd to use a bind mount as the source of flags.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="fileSource">The path to the flag configuration file on the host.</param>
    /// <param name="filename">The name of the flag configuration file. Defaults to "flagd.json".</param>
    /// <returns>The <see cref="IResourceBuilder{FlagdResource}"/>.</returns>
    public static IResourceBuilder<FlagdResource> WithBindFileSync(
        this IResourceBuilder<FlagdResource> builder,
        string fileSource,
        string filename = "flagd.json")
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(fileSource, nameof(fileSource));

        return builder
            .WithBindMount(fileSource, "/flags/")
            .WithArgs("--uri", $"file:./flags/{filename}");
    }
}
