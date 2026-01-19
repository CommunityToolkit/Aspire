using Aspire.Hosting.ApplicationModel;
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

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/root/.dbgate", isReadOnly);
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
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency. Optional; defaults to <c>dbgate</c>.</param>
    /// <param name="port">The host port to bind the underlying container to.</param>
    /// <remarks>
    /// Multiple <see cref="AddDbGate(IDistributedApplicationBuilder, string, int?)"/> calls will return the same resource builder instance.
    /// </remarks>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<DbGateContainerResource> AddDbGate(this IDistributedApplicationBuilder builder, [ResourceName] string name = "dbgate", int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

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
                                               .WithHttpEndpoint(targetPort: 3000, port: port, name: DbGateContainerResource.PrimaryEndpointName)
                                               .WithUrlForEndpoint(DbGateContainerResource.PrimaryEndpointName, e => e.DisplayText = "DbGate Dashboard")
                                               .WithIconName("WindowDatabase")
                                               .ExcludeFromManifest();

            return dbGateContainerBuilder;
        }
    }

    /// <summary>
    /// Sanitizes a resource name to be used as a connection ID in DbGate environment variables.
    /// </summary>
    /// <param name="resourceName">The resource name to sanitize.</param>
    /// <returns>A sanitized connection ID safe for use in environment variable names.</returns>
    /// <remarks>
    /// <para>
    /// This method performs basic sanitization by replacing hyphens with underscores, as hyphens are not valid
    /// in Linux environment variable names.
    /// </para>
    /// <para>
    /// Note: Linux environment variable names have additional constraints (must contain only letters, numbers, and underscores,
    /// and cannot start with a number). This method does not validate or enforce these additional constraints.
    /// Resource names should follow standard naming conventions to ensure compatibility.
    /// </para>
    /// </remarks>
    public static string SanitizeConnectionId(string resourceName)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        return resourceName.Replace('-', '_');
    }
}
