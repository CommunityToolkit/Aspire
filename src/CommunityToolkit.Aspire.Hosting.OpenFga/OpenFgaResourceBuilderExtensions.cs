using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding OpenFGA resources to the application model.
/// </summary>
public static class OpenFgaResourceBuilderExtensions
{
    /// <summary>
    /// Adds an OpenFGA container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The name of the resource. This name will be used as the connection string name when referenced in a dependency.</param>
    /// <param name="httpPort">An optional fixed port for the HTTP endpoint. If not specified, a random port will be assigned.</param>
    /// <param name="grpcPort">An optional fixed port for the gRPC endpoint. If not specified, a random port will be assigned.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OpenFgaResource> AddOpenFga(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        int? grpcPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(name, nameof(name));

        var resource = new OpenFgaResource(name);

        return builder.AddResource(resource)
            .WithImage(OpenFgaContainerImageTags.Image, OpenFgaContainerImageTags.Tag)
            .WithImageRegistry(OpenFgaContainerImageTags.Registry)
            .WithHttpEndpoint(port: httpPort, targetPort: 8080, name: OpenFgaResource.HttpEndpointName)
            .WithEndpoint(port: grpcPort, targetPort: 8081, name: OpenFgaResource.GrpcEndpointName, scheme: "http")
            .WithArgs("run")
            .WithHttpHealthCheck("/healthz");
    }

    /// <summary>
    /// Adds a data volume to the OpenFGA container.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="name">The name of the volume. Defaults to an auto-generated name based on the application and resource names.</param>
    /// <param name="isReadOnly">A flag that indicates if this is a read-only volume.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OpenFgaResource> WithDataVolume(
        this IResourceBuilder<OpenFgaResource> builder,
        string? name = null,
        bool isReadOnly = false)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.WithVolume(
            name ?? VolumeNameGenerator.Generate(builder, "data"),
            "/data",
            isReadOnly);
    }

    /// <summary>
    /// Configures OpenFGA to use PostgreSQL as the datastore.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="database">The PostgreSQL database resource to use.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OpenFgaResource> WithPostgresDatastore(
        this IResourceBuilder<OpenFgaResource> builder,
        IResourceBuilder<IResourceWithConnectionString> database)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(database, nameof(database));

        return builder
            .WithEnvironment("OPENFGA_DATASTORE_ENGINE", "postgres")
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["OPENFGA_DATASTORE_URI"] = database.Resource.ConnectionStringExpression;
            })
            .WaitFor(database);
    }

    /// <summary>
    /// Configures OpenFGA to use MySQL as the datastore.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="database">The MySQL database resource to use.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OpenFgaResource> WithMySqlDatastore(
        this IResourceBuilder<OpenFgaResource> builder,
        IResourceBuilder<IResourceWithConnectionString> database)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(database, nameof(database));

        return builder
            .WithEnvironment("OPENFGA_DATASTORE_ENGINE", "mysql")
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["OPENFGA_DATASTORE_URI"] = database.Resource.ConnectionStringExpression;
            })
            .WaitFor(database);
    }

    /// <summary>
    /// Configures OpenFGA to use in-memory storage for the datastore.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    /// <remarks>
    /// In-memory storage is suitable for development and testing only. Data will be lost when the container restarts.
    /// </remarks>
    public static IResourceBuilder<OpenFgaResource> WithInMemoryDatastore(
        this IResourceBuilder<OpenFgaResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        return builder.WithEnvironment("OPENFGA_DATASTORE_ENGINE", "memory");
    }

    /// <summary>
    /// Enables experimental features in OpenFGA.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{T}"/>.</param>
    /// <param name="features">A comma-separated list of experimental features to enable.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    public static IResourceBuilder<OpenFgaResource> WithExperimentalFeatures(
        this IResourceBuilder<OpenFgaResource> builder,
        string features)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentException.ThrowIfNullOrEmpty(features, nameof(features));

        return builder.WithEnvironment("OPENFGA_EXPERIMENTALS", features);
    }
}
