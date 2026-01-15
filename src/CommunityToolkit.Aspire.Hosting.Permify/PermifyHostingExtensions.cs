using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Permify;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Permify to an <see cref="IDistributedApplicationBuilder" />.
/// </summary>
public static class PermifyHostingExtensions
{
    /// <summary>
    /// Adds a Permify resource to the application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the Permify resource.</param>
    /// <param name="httpPort">The HTTP port for Permify.</param>
    /// <param name="grpcPort">The gRPC port for Permify.</param>
    public static IResourceBuilder<PermifyResource> AddPermify(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        int? httpPort = null,
        int? grpcPort = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        var resource = new PermifyResource(name);
        var permifyBuilder = builder.AddResource(resource)
            .WithImage(PermifyContainerImageTags.Image)
            .WithImageTag(PermifyContainerImageTags.Tag)
            .WithImageRegistry(PermifyContainerImageTags.Registry)
            // Configure endpoints
            .WithHttpsEndpoint(
                targetPort: 8080,
                port: httpPort,
                name: PermifyResource.HttpsEndpointName
            )
            .WithEnvironment("PERMIFY_HTTP_ENABLED", "true")
            .WithEnvironment("PERMIFY_HTTP_PORT", "8080")
            .WithHttpHealthCheck("/healthz")
            // Configure OTLP
            .WithOtlpExporter()
            .WithEnvironment("PERMIFY_TRACER_ENABLED", "true")
            .WithEnvironment("PERMIFY_TRACER_EXPORTER", "otlp")
            .WithEnvironment("PERMIFY_METER_ENABLED", "true")
            .WithEnvironment("PERMIFY_METER_EXPORTER", "otlp")
            .WithEnvironment(ctx =>
            {
                // TODO: Permify requires the endpoint to *just* be the host + port
                // it cannot contain a scheme, which makes it difficult to use HostUrl
                ctx.EnvironmentVariables["PERMIFY_TRACER_ENDPOINT"] = string.Empty;
                ctx.EnvironmentVariables["PERMIFY_METER_ENDPOINT"] = string.Empty;
            });

#pragma warning disable ASPIRECERTIFICATES001
        permifyBuilder.WithHttpsCertificateConfiguration(ctx =>
        {
            // Configure HTTPS
            ctx.EnvironmentVariables["PERMIFY_HTTP_TLS_ENABLED"] = true;
            ctx.EnvironmentVariables["PERMIFY_HTTP_TLS_CERT_PATH"] = ctx.CertificatePath;
            ctx.EnvironmentVariables["PERMIFY_HTTP_TLS_KEY_PATH"] = ctx.KeyPath;

            // Configure gRPC
            ctx.EnvironmentVariables["PERMIFY_GRPC_TLS_ENABLED"] = true;
            ctx.EnvironmentVariables["PERMIFY_GRPC_TLS_CERT_PATH"] = ctx.CertificatePath;
            ctx.EnvironmentVariables["PERMIFY_GRPC_TLS_KEY_PATH"] = ctx.KeyPath;

            return Task.CompletedTask;
        });
#pragma warning restore ASPIRECERTIFICATES001

        return permifyBuilder;
    }

    /// <summary>
    /// Enables gRPC support for Permify.
    /// </summary>
    /// <param name="builder">The resource to enable gRPC support for</param>
    /// <param name="grpcPort">An optional port on which to enable gRPC</param>
    public static IResourceBuilder<PermifyResource> WithGrpc(
        this IResourceBuilder<PermifyResource> builder,
        int? grpcPort = null
    )
    {
        return builder
            .WithHttpsEndpoint(
                targetPort: 8081,
                port: grpcPort,
                name: PermifyResource.GrpcEndpointName
            )
            .WithEnvironment("PERMIFY_GRPC_ENABLED", "true")
            .WithEnvironment("PERMIFY_GRPC_PORT", "8081");
    }

    /// <summary>
    /// Adds <paramref name="database"/> as the database for Permify to persist data.
    /// </summary>
    public static IResourceBuilder<PermifyResource> WithDatabase(
        this IResourceBuilder<PermifyResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        builder
            .WithEnvironment("PERMIFY_DATABASE_ENGINE", "postgres")
            .WithEnvironment("PERMIFY_DATABASE_URI", database.Resource.UriExpression)
            .WaitFor(database);

        return builder;
    }

    /// <summary>
    /// Configures Permify to enable watch support.
    /// </summary>
    /// <remarks>
    /// Permify's watch support relies on Postgres' <c>track_commit_timestamp</c> feature.
    /// Refer to https://docs.permify.co/api-reference/watch/watch-changes#enabling-track-commit-timestamp-on-postgresql
    /// for information on how to configure this, or call <see cref="WithWatchSupport(IResourceBuilder{PermifyResource}, IResourceBuilder{PostgresServerResource}, IResourceBuilder{PostgresDatabaseResource})"/>
    /// to automatically configure the flag for you.
    /// </remarks>
    public static IResourceBuilder<PermifyResource> WithWatchSupport(
        this IResourceBuilder<PermifyResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        return WithDatabase(builder, database)
            .WithEnvironment("PERMIFY_SERVICE_WATCH_ENABLED", "true");
    }

    /// <summary>
    /// Configures permify to enable watch support.
    /// The passed <paramref name="server"/> resource will be modified to enable <c>track_commit_timestamp</c>.
    /// If you don't want this, use <see cref="WithWatchSupport(IResourceBuilder{PermifyResource}, IResourceBuilder{PostgresDatabaseResource})"/> instead.
    /// </summary>
    public static IResourceBuilder<PermifyResource> WithWatchSupport(
        this IResourceBuilder<PermifyResource> builder,
        IResourceBuilder<PostgresServerResource> server,
        IResourceBuilder<PostgresDatabaseResource> database
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(database);

        server.WithArgs("-c", "track_commit_timestamp=on");
        return builder.WithWatchSupport(database);
    }
}