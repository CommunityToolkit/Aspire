using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for configuring and managing Logto  resources
/// within the Aspire hosting application framework.
/// </summary>
public static class LogtoBuilderExtensions
{
    /// <summary>
    /// Adds a Logto resource to the Aspire distributed application by configuring it
    /// with the specified name, associated PostgreSQL server resource, and database name.
    /// </summary>
    /// <param name="builder">The distributed application builder to which the Logto  resource will be added.</param>
    /// <param name="name">The name of the Logto  resource.</param>
    /// <param name="postgres">The resource builder for the PostgreSQL server that the Logto  will connect to.</param>
    /// <param name="databaseName">The PostgreSQL database name Logto should use.</param>
    /// <param name="port">The host port to be configured for the primary endpoint. If <see langword="null"/>, Aspire will assign a random host port.</param>
    /// <param name="adminPort">The host port to be configured for the administrative endpoint. If <see langword="null"/>, Aspire will assign a random host port.</param>
    /// <returns>The resource builder configured for the added Logto  resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> AddLogto(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<PostgresServerResource> postgres,
        string databaseName = "logto_db",
        int? port = null,
        int? adminPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(postgres);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);


        var resource = new LogtoResource(name);
        var builderWithResource = builder
            .AddResource(resource)
            .WithImage(LogtoContainerImageTags.Image, LogtoContainerImageTags.Tag)
            .WithImageRegistry(LogtoContainerImageTags.Registry);

        builderWithResource.WithResourcePort(port, adminPort);
        builderWithResource.WithDatabase(postgres, databaseName);
        SetHealthCheck(builder, builderWithResource, name);

        return builderWithResource;
    }

    /// <summary>
    /// Enables Node.js deprecation tracing for the Logto by setting the
    /// NODE_OPTIONS environment variable to '--trace-deprecation'.
    /// This allows stack traces to be printed for deprecated API usage.
    /// </summary>
    /// <param name="builderWithResource">The resource builder for the Logto resource that will be configured for stack trace logging.</param>
    /// <returns>The resource builder for the configured Logto resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithDeprecationTracing(this IResourceBuilder<LogtoResource> builderWithResource)
    {
        ArgumentNullException.ThrowIfNull(builderWithResource);

        return builderWithResource.WithEnvironment("NODE_OPTIONS", "--trace-deprecation");
    }

    private static void SetHealthCheck(IDistributedApplicationBuilder builder,
        IResourceBuilder<LogtoResource> builderWithResource, string name)
    {
        var endpoint = builderWithResource.Resource.GetEndpoint(LogtoResource.PrimaryEndpointName);
        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks()
            .AddUrlGroup(opt =>
            {
                var uri = new Uri(endpoint.Url);
                opt.AddUri(new Uri(uri, "/api/status"), setup => setup.ExpectHttpCode(204));
            }, healthCheckKey);
        builderWithResource.WithHealthCheck(healthCheckKey);
    }

    /// <summary>
    /// Configures the Logto  resource to use the specified Node.js environment value
    /// by setting the corresponding environment variable.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="env">The value of the Node.js environment variable to set, typically "development", "production", or "test".</param>
    /// <returns>The resource builder for the configured Logto  resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithNodeEnv(this IResourceBuilder<LogtoResource> builder, string env)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(env);

        return builder.WithEnvironment("NODE_ENV", env);
    }

    /// <summary>
    /// Configures the Logto resource with a data volume, allowing persistent storage.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="name">The optional name of the data volume. If not provided, a default name is generated.</param>
    /// <returns>The resource builder configured with the specified data volume.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithDataVolume(this IResourceBuilder<LogtoResource> builder, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/data");
    }

    /// <summary>
    /// Configures HTTP endpoints for the given Logto resource builder with specified port settings.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="port">The host port to be configured for the primary endpoint. If <see langword="null"/>, Aspire will assign a random host port.</param>
    /// <param name="adminPort">The host port to be configured for the administrative endpoint. If <see langword="null"/>, Aspire will assign a random host port.</param>
    /// <returns>The updated resource builder with the configured HTTP endpoints.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithResourcePort(
        this IResourceBuilder<LogtoResource> builder,
        int? port = null,
        int? adminPort = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithHttpEndpoint(
                port: port,
                targetPort: LogtoResource.DefaultHttpPort,
                name: LogtoResource.PrimaryEndpointName)
            .WithHttpEndpoint(
                port: adminPort,
                targetPort: LogtoResource.DefaultHttpAdminPort,
                name: LogtoResource.AdminEndpointName);
    }

    /// <summary>
    /// Configures the specified Logto resource to include an administrative endpoint with the given URL.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="url">The URL of the administrative endpoint to be used for the Logto resource.</param>
    /// <returns>The resource builder for the configured Logto resource.</returns>
    /// <example>https://admin.domain.com</example>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithAdminEndpoint(this IResourceBuilder<LogtoResource> builder, string url)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(url);

        return builder.WithEnvironment("ADMIN_ENDPOINT", url);
    }

    /// <summary>
    /// Configures the Logto resource to disable the Admin Console port.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="disable">A value indicating whether to disable the Admin Console port.</param>
    /// <returns>The resource builder for the configured Logto resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithDisableAdminConsole(this IResourceBuilder<LogtoResource> builder, bool disable)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEnvironment("ADMIN_DISABLE_LOCALHOST", disable.ToString());
    }

    /// <summary>
    /// Configures the Logto resource to enable or disable the trust proxy header behavior.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="trustProxyHeader">A value indicating whether to trust the proxy header.</param>
    /// <returns>The resource builder for the configured Logto resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithTrustProxyHeader(this IResourceBuilder<LogtoResource> builder, bool trustProxyHeader)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEnvironment("TRUST_PROXY_HEADER", trustProxyHeader.ToString());
    }

    /// <summary>
    /// Specifies whether the username is case-sensitive.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="sensitiveUsername">A value indicating whether usernames should be treated as case-sensitive.</param>
    /// <returns>The updated resource builder with the configured case-sensitivity setting.</returns>
    [AspireExport]
    [Obsolete("CASE_SENSITIVE_USERNAME is deprecated in Logto 1.41. Configure username case sensitivity per tenant in the Logto Console instead.")]
    public static IResourceBuilder<LogtoResource> WithSensitiveUsername(this IResourceBuilder<LogtoResource> builder, bool sensitiveUsername)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithEnvironment("CASE_SENSITIVE_USERNAME", sensitiveUsername.ToString());
    }

    /// <summary>
    /// Configures the Logto resource to use a secret vault with the specified key encryption key (KEK).
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="secretVaultKek">The base64-encoded key encryption key (KEK) for the secret vault.</param>
    /// <returns>The resource builder for the configured Logto resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithSecretVault(this IResourceBuilder<LogtoResource> builder, string secretVaultKek)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(secretVaultKek);

        return builder.WithEnvironment("SECRET_VAULT_KEK", secretVaultKek);
    }

    /// <summary>
    /// Configures the Logto resource to use a data bind mount with the specified source directory.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="source">The host directory to be mounted as the data volume.</param>
    /// <returns>The resource builder for the configured Logto resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithDataBindMount(this IResourceBuilder<LogtoResource> builder, string source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(source);

        return builder.WithBindMount(source, "/data");
    }

    /// <summary>
    /// Configures the Logto resource to use a specified Redis resource for caching or other functionality.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="redis">The resource builder for the Redis resource to be used by the Logto resource.</param>
    /// <returns>The resource builder configured with the specified Redis resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithRedis(this IResourceBuilder<LogtoResource> builder, IResourceBuilder<RedisResource> redis)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(redis);

        return builder.WithEnvironment("REDIS_URL", redis.Resource.UriExpression)
            .WaitFor(redis);
    }

    /// <summary>
    /// Configures the Logto resource to connect to the specified PostgreSQL database.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <param name="postgres">The resource builder for the PostgreSQL server to connect to.</param>
    /// <param name="databaseName">The PostgreSQL database name Logto should use.</param>
    /// <returns>The resource builder for the configured Logto resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithDatabase(
        this IResourceBuilder<LogtoResource> builder,
        IResourceBuilder<PostgresServerResource> postgres,
        string databaseName = "logto_db")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(postgres);
        ArgumentException.ThrowIfNullOrEmpty(databaseName);

        var dbUrlBuilder = new ReferenceExpressionBuilder();
        dbUrlBuilder.Append($"{postgres.Resource.UriExpression}/{databaseName}");
        var dbUrl = dbUrlBuilder.Build();

        return builder.WithEnvironment("DB_URL", dbUrl)
            .WaitFor(postgres);
    }

    /// <summary>
    /// Starts Logto by running the database seed command before the application process.
    /// </summary>
    /// <param name="builder">The resource builder for the Logto resource to configure.</param>
    /// <returns>The resource builder for the configured Logto resource.</returns>
    [AspireExport]
    public static IResourceBuilder<LogtoResource> WithDatabaseSeeding(this IResourceBuilder<LogtoResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithEntrypoint("sh")
            .WithArgs("-c", "npm run cli db seed -- --swe && npm start");
    }
}

#pragma warning restore ASPIREATS001
