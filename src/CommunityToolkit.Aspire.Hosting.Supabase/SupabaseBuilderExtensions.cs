using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Supabase;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Supabase resources to the application model.
/// </summary>
public static class SupabaseBuilderExtensions
{
    private const int SupabaseApiPort = 8000;
    private const int SupabaseDatabasePort = 5432;

    /// <summary>
    /// Adds a Supabase core Postgres and HTTP API container to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the Supabase resource.</param>
    /// <param name="password">Optional password parameter resource builder.</param>
    /// <param name="apiPort">Optional host port for the Supabase HTTP API.</param>
    /// <param name="dbPort">Optional host port for the PostgreSQL database.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> AddSupabase(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource>? password = null,
        int? apiPort = 8080,
        int? dbPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);

        ParameterResource passwordParam = password?.Resource ?? ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-password");
        SupabaseResource resource = new(name, passwordParam);

        return builder.AddResource(resource)
            .WithImage(SupabaseContainerImageTags.PostgresImage, SupabaseContainerImageTags.PostgresTag)
            .WithImageRegistry(SupabaseContainerImageTags.Registry)
            .WithHttpEndpoint(targetPort: SupabaseApiPort, port: apiPort, name: SupabaseResource.PrimaryEndpointName)
            .WithEndpoint(targetPort: SupabaseDatabasePort, port: dbPort, name: SupabaseResource.DatabaseEndpointName)
            .WithEnvironment(context =>
            {
                context.EnvironmentVariables["POSTGRES_PASSWORD"] = resource.PasswordParameter;
                context.EnvironmentVariables["POSTGRES_DB"] = "postgres";
                context.EnvironmentVariables["POSTGRES_USER"] = "postgres";
                context.EnvironmentVariables["PGDATA"] = "/var/lib/postgresql/data";
                context.EnvironmentVariables["PGHOST"] = resource.DatabaseEndpoint.Property(EndpointProperty.Host);
                context.EnvironmentVariables["PGPORT"] = resource.DatabaseEndpoint.Property(EndpointProperty.Port);
                context.EnvironmentVariables["PGPASSWORD"] = resource.PasswordParameter;
                context.EnvironmentVariables["PGDATABASE"] = "postgres";
            })
            .WithDataBindMount("./volumes/db/data")
            .WithBindMount("./volumes/db/migrations", "/docker-entrypoint-initdb.d/migrations")
            .WithBindMount("./volumes/db/init-scripts", "/docker-entrypoint-initdb.d/init-scripts");
    }

    /// <summary>
    /// Adds all Supabase modules (child services) to the Supabase resource.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the Supabase resource.</param>
    /// <param name="password">Optional password parameter resource builder.</param>
    /// <param name="apiPort">Optional host port for the Supabase HTTP API.</param>
    /// <param name="dbPort">Optional host port for the PostgreSQL database.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> AddAllSupabase(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource>? password = null,
        int? apiPort = null,
        int? dbPort = null)
        => builder.AddSupabase(name, password, apiPort, dbPort)
            .WithKong()
            .WithStudio()
            .WithRest()
            .WithRealtime()
            .WithStorageService()
            .WithAuthService()
            .WithMetaService()
            .WithInbucketService()
            .WithImageProxyService()
            .WithLogflareService()
            .WithEdgeRuntimeService();

    // Individual module extension methods
    private static IResourceBuilder<SupabaseResource> WithModule(
        this IResourceBuilder<SupabaseResource> builder,
        string suffix,
        string image,
        string tag)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var resource = builder.Resource;
        var container = new ContainerResource($"{resource.Name}-{suffix}");
        builder.ApplicationBuilder.AddResource(container)
            .WithImage(image, tag)
            .WithImageRegistry(SupabaseContainerImageTags.Registry)
            .WithParentRelationship(resource);
        return builder;
    }

    /// <summary>
    /// Adds the Kong gateway module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithKong(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("kong", SupabaseContainerImageTags.KongImage, SupabaseContainerImageTags.KongTag)
            .WithHttpEndpoint(targetPort: 8000, port: null, name: $"{builder.Resource.Name}-kong");
    }

    /// <summary>
    /// Adds the Studio module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithStudio(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("studio", SupabaseContainerImageTags.StudioImage, SupabaseContainerImageTags.StudioTag)
            .WithHttpEndpoint(targetPort: 3000, port: null, name: $"{builder.Resource.Name}-studio");
    }

    /// <summary>
    /// Adds the REST module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithRest(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("rest", SupabaseContainerImageTags.RestImage, SupabaseContainerImageTags.RestTag)
            .WithHttpEndpoint(targetPort: 3000, port: null, name: $"{builder.Resource.Name}-rest");
    }

    /// <summary>
    /// Adds the Realtime module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithRealtime(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("realtime", SupabaseContainerImageTags.RealtimeImage, SupabaseContainerImageTags.RealtimeTag)
            .WithHttpEndpoint(targetPort: 4000, port: null, name: $"{builder.Resource.Name}-realtime");
    }

    /// <summary>
    /// Adds the Storage module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithStorageService(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("storage", SupabaseContainerImageTags.StorageImage, SupabaseContainerImageTags.StorageTag)
            .WithHttpEndpoint(targetPort: 5000, port: null, name: $"{builder.Resource.Name}-storage");
    }

    /// <summary>
    /// Adds the Auth module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithAuthService(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("auth", SupabaseContainerImageTags.AuthImage, SupabaseContainerImageTags.AuthTag)
            .WithHttpEndpoint(targetPort: 9999, port: null, name: $"{builder.Resource.Name}-auth");
    }

    /// <summary>
    /// Adds the Meta module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithMetaService(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("meta", SupabaseContainerImageTags.MetaImage, SupabaseContainerImageTags.MetaTag)
            .WithHttpEndpoint(targetPort: 8080, port: null, name: $"{builder.Resource.Name}-meta");
    }

    /// <summary>
    /// Adds the Inbucket module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithInbucketService(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("inbucket", SupabaseContainerImageTags.InbucketImage, SupabaseContainerImageTags.InbucketTag)
            .WithHttpEndpoint(targetPort: 9000, port: null, name: $"{builder.Resource.Name}-inbucket");
    }

    /// <summary>
    /// Adds the Image Proxy module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithImageProxyService(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("image-proxy", SupabaseContainerImageTags.ImageProxyImage, SupabaseContainerImageTags.ImageProxyTag)
            .WithHttpEndpoint(targetPort: 5001, port: null, name: $"{builder.Resource.Name}-image-proxy");
    }

    /// <summary>
    /// Adds the Logflare module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithLogflareService(
        this IResourceBuilder<SupabaseResource> builder,
        int? port = 4000)
    {
        var builderUniqueName = builder.Resource.Name;
        var endpointName = $"{builderUniqueName}-logflare";
        return builder.WithModule("logflare", SupabaseContainerImageTags.LogflareImage, SupabaseContainerImageTags.LogflareTag)
            .WithHttpEndpoint(targetPort: 4000, port: port, name: endpointName);
    }

    /// <summary>
    /// Adds the Edge Runtime module to the Supabase resource.
    /// </summary>
    public static IResourceBuilder<SupabaseResource> WithEdgeRuntimeService(this IResourceBuilder<SupabaseResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithModule("edge-runtime", SupabaseContainerImageTags.EdgeRuntimeImage, SupabaseContainerImageTags.EdgeRuntimeTag)
            .WithHttpEndpoint(targetPort: 8000, port: null, name: $"{builder.Resource.Name}-edge-runtime");
    }

    /// <summary>
    /// Adds a named volume for the data folder to a Supabase container resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <param name="name">Optional name for the volume.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithDataVolume(
        this IResourceBuilder<SupabaseResource> builder,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/var/lib/postgresql/data");
    }

    /// <summary>
    /// Adds a bind mount for the data folder to a Supabase container resource.
    /// </summary>
    /// <param name="builder">The <see cref="IResourceBuilder{SupabaseResource}"/>.</param>
    /// <param name="source">The source path on the host machine.</param>
    /// <returns>A <see cref="IResourceBuilder{SupabaseResource}"/> for further configuration.</returns>
    public static IResourceBuilder<SupabaseResource> WithDataBindMount(
        this IResourceBuilder<SupabaseResource> builder,
        string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(source);
        return builder.WithBindMount(source, "/var/lib/postgresql/data");
    }
}