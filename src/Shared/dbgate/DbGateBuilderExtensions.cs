using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.DbGate;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for DbGate resources to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class DbGateBuilderExtensions
{
    /// <summary>
    /// Configures the host port that the DbGate resource is exposed on instead of using randomly assigned port.
    /// </summary>
    /// <param name="builder">The resource builder for DbGate.</param>
    /// <param name="port">The port to bind on the host. If <see langword="null"/> is used random port will be assigned.</param>
    /// <returns>The resource builder for DbGate.</returns>
    public static IResourceBuilder<DbGateContainerResource> WithHostPort(this IResourceBuilder<DbGateContainerResource> builder, int? port)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEndpoint(DbGateContainerResource.PrimaryEndpointName, endpoint =>
        {
            endpoint.Port = port;
        });
    }

    /// <summary>
    /// Adds a named volume for the data folder to a DbGate container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DbGateContainerResource> WithDataVolume(this IResourceBuilder<DbGateContainerResource> builder, string? name = null, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

#pragma warning disable CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return builder.WithVolume(name ?? VolumeNameGenerator.CreateVolumeName(builder, "data"),
            "/root/.dbgate", isReadOnly);
#pragma warning restore CTASPIRE001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a DbGate container resource.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="source">The source directory on the host to mount into the container.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only mount.</param>
    /// <returns>The <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DbGateContainerResource> WithDataBindMount(this IResourceBuilder<DbGateContainerResource> builder, string source, bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);

        return builder.WithBindMount(source, "/root/.dbgate", isReadOnly);
    }

    /// <summary>
    /// Adds a DbGate container resource to the application.
    /// </summary>
    /// <param name="builder">The resource builder.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DbGateContainerResource> AddDbGate(this IDistributedApplicationBuilder builder, [ResourceName] string name)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.Resources.OfType<DbGateContainerResource>().SingleOrDefault() is { } existingDbGateResource)
        {
            var builderForExistingResource = builder.CreateResourceBuilder(existingDbGateResource);
            return builderForExistingResource;
        }
        else
        {
            var dbGateContainer = new DbGateContainerResource(name);
            var dbGateContainerBuilder = builder.AddResource(dbGateContainer)
                                               .WithImage(DbGateContainerImageTags.Image, DbGateContainerImageTags.Tag)
                                               .WithImageRegistry(DbGateContainerImageTags.Registry)
                                               .WithHttpEndpoint(targetPort: 3000, name: DbGateContainerResource.PrimaryEndpointName)
                                               .ExcludeFromManifest();

            return dbGateContainerBuilder;
        }
    }
}
