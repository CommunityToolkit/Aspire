using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding ClamAv resources to the application model.
/// </summary>
public static class ClamAvBuilderExtensions
{

    /// <summary>
    /// Adds a ClamAv container resource to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClamAvResource> AddClamAv(
        this IDistributedApplicationBuilder builder,
        string name,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var clamAv = new ClamAvResource(name);
        builder.Eventing.Subscribe<ConnectionStringAvailableEvent>(clamAv, async (@event, ct) =>
        {
            var connectionString = await clamAv.ConnectionStringExpression.GetValueAsync(ct).ConfigureAwait(false)
            ?? throw new DistributedApplicationException($"ConnectionStringAvailableEvent was published for the '{clamAv.Name}' resource but the connection string was null.");

            // todo: health check

        });
        return builder.AddResource(clamAv)
            .WithImage(ClamAvContainerImageTags.Image, ClamAvContainerImageTags.Tag)
            .WithImageRegistry(ClamAvContainerImageTags.Registry)
            .WithEnvironment("CLAMAV_NO_FRESHCLAMD", builder.ExecutionContext.IsRunMode.ToString())
            .WithEndpoint(port: port, name: ClamAvResource.PrimaryEndpointName, targetPort: 3310)
            ;
    }

    /// <summary>
    /// Add a named volume for the ClamAv data directory.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume.  Defaults to an auto-generated name based on the application and resource names.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClamAvResource> WithDataVolume(this IResourceBuilder<ClamAvResource> builder, string? name = null)
    {

        ArgumentNullException.ThrowIfNull(builder);

#pragma warning disable CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return builder.WithVolume(name ?? VolumeNameGenerator.CreateVolumeName(builder, "data"), "/var/lib/clamav", false);
#pragma warning restore CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    }

    /// <summary>
    /// Add a bind mount for the ClamAv data directory.
    /// </summary>
    /// <param name="builder">The resource builder</param>
    /// <param name="source">The source directory on the host to mount</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<ClamAvResource> WithDataBindMount(this IResourceBuilder<ClamAvResource> builder, string source)
    {

        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/var/lib/clamav", false);
    }


}

