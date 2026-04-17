using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Logto;

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
    /// <returns>The resource builder configured for the added Logto  resource.</returns>
    public static IResourceBuilder<LogtoResource> AddLogto(
        this IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<PostgresServerResource> postgres)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(postgres);


        var resource = new LogtoResource(name);
        var builderWithResource = builder
            .AddResource(resource)
            .WithImage(LogtoTags.Image, LogtoTags.Tag)
            .WithImageRegistry(LogtoTags.Registry);

        builderWithResource.WithResourcePort();
        builderWithResource.WithDatabase(postgres);
        SetHealthCheck(builder, builderWithResource, name);


        builderWithResource
            .WithEntrypoint("sh")
            .WithArgs("-c", "npm run cli db seed -- --swe && npm start");


        return builderWithResource;
    }

    /// <summary>
    /// Enables Node.js deprecation tracing for the Logto by setting the
    /// NODE_OPTIONS environment variable to '--trace-deprecation'.
    /// This allows stack traces to be printed for deprecated API usage.
    /// </summary>
    /// <param name="builderWithResource">The resource builder for the Logto resource that will be configured for stack trace logging.</param>
    public static void WithDeprecationTracing(this IResourceBuilder<LogtoResource> builderWithResource)
    {
        builderWithResource.WithEnvironment("NODE_OPTIONS", "--trace-deprecation");
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

    /// <param name="builder">The resource builder for the Logto  resource to be configured.</param>
    extension(IResourceBuilder<LogtoResource> builder)
    {
        /// <summary>
        /// Configures the Logto  resource to use the specified Node.js environment value
        /// by setting the corresponding environment variable.
        /// </summary>
        /// <param name="env">The value of the Node.js environment variable to set, typically "development", "production", or "test".</param>
        /// <returns>The resource builder for the configured Logto  resource.</returns>
        public IResourceBuilder<LogtoResource> WithNodeEnv(string env)
        {
            return builder.WithEnvironment("NODE_ENV", env);
        }

        /// <summary>
        /// Configures the Logto  resource with a data volume, allowing persistent storage
        /// for the .
        /// </summary>
        /// <param name="name">The optional name of the data volume. If not provided, a default name is generated.</param>
        /// <returns>The resource builder configured with the specified data volume.</returns>
        public IResourceBuilder<LogtoResource> WithDataVolume(string? name = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            return builder.WithVolume(name ?? VolumeNameGenerator.Generate(builder, "data"), "/data");
        }


        /// <summary>
        /// Configures HTTP endpoints for the given Logto  resource builder with specified port settings.
        /// </summary>
        /// <param name="port">The host port to be configured for the primary endpoint. If <see langword="null"/>, Aspire will assign a random host port.</param>
        /// <param name="adminPort">The host port to be configured for the administrative endpoint. If <see langword="null"/>, Aspire will assign a random host port.</param>
        /// <returns>The updated resource builder with the configured HTTP endpoints.</returns>
        public IResourceBuilder<LogtoResource> WithResourcePort(
            int? port = LogtoResource.DefaultHttpPort,
            int? adminPort = LogtoResource.DefaultHttpAdminPort)
        {
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
        /// Configures the specified Logto  resource to include an administrative endpoint
        /// with the given URL.
        /// </summary>
        /// <param name="url">The URL of the administrative endpoint to be used for the Logto  resource.</param>
        /// <returns>The resource builder for the configured Logto  resource.</returns>
        /// <example>https://admin.domain.com</example>
        public IResourceBuilder<LogtoResource> WithAdminEndpoint(string url)
        {
            return builder.WithEnvironment("ADMIN_ENDPOINT", url);
        }

        /// <summary>
        /// Configures the Logto  resource to disable the Admin Console port.
        /// When set to true and ADMIN_ENDPOINT is unset, it will completely disable the Admin Console.
        /// </summary>
        /// <param name="disable">
        /// A boolean value indicating whether to disable the Admin Console port.
        /// Set to true to disable the port for Admin Console; otherwise, false.
        /// With ADMIN_ENDPOINT unset, setting this to true will completely disable the Admin Console.
        /// </param>
        /// <returns>The resource builder for the configured Logto  resource.</returns>
        public IResourceBuilder<LogtoResource> WithDisableAdminConsole(bool disable)
        {
            return builder.WithEnvironment("ADMIN_DISABLE_LOCALHOST", disable.ToString());
        }

        /// <summary>
        /// Configures the Logto  resource to enable or disable the trust proxy header behavior
        /// based on the specified value.
        /// </summary>
        /// <param name="trustProxyHeader">
        /// A boolean value indicating whether to trust the proxy header.
        /// Set to true to trust the proxy header; otherwise, false.
        /// </param>
        /// <returns>The resource builder for the configured Logto  resource.</returns>
        public IResourceBuilder<LogtoResource> WithTrustProxyHeader(bool trustProxyHeader)
        {
            return builder.WithEnvironment("TRUST_PROXY_HEADER", trustProxyHeader.ToString());
        }

        /// <summary>
        /// Specifies whether the username is case-sensitive.
        /// </summary>
        /// <param name="sensitiveUsername">A value indicating whether usernames should be treated as case-sensitive.</param>
        /// <returns>The updated resource builder with the configured case-sensitivity setting.</returns>
        public IResourceBuilder<LogtoResource> WithSensitiveUsername(bool sensitiveUsername)
        {
            return builder.WithEnvironment("CASE_SENSITIVE_USERNAME", sensitiveUsername.ToString());
        }

        /// <summary>
        /// Configures the Logto  resource to use a secret vault with the specified key encryption key (KEK).
        /// The KEK is used to encrypt Data Encryption Keys (DEK) in the Secret Vault and must be a base64-encoded string.
        /// AES-256 (32 bytes) is recommended. Example: <c>crypto.randomBytes(32).toString('base64')</c>
        /// </summary>
        /// <param name="secretVaultKek">The base64-encoded key encryption key (KEK) for the secret vault. Must be base64-encoded; AES-256 (32 bytes) is recommended.</param>
        public IResourceBuilder<LogtoResource> WithSecretVault(string secretVaultKek)
        {
            return builder.WithEnvironment("SECRET_VAULT_KEK", secretVaultKek);
        }

        /// <summary>
        /// Configures the Logto  resource to use a data bind mount with the specified
        /// source directory as the data volume for the .
        /// </summary>
        /// <param name="source">The host directory to be mounted as the 's data volume.</param>
        /// <returns>The resource builder for the configured Logto  resource.</returns>
        public IResourceBuilder<LogtoResource> WithDataBindMount(string source)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(source);

            return builder.WithBindMount(source, "/data");
        }

        /// <summary>
        /// Configures the Logto  resource to use a specified Redis resource for caching or other functionality
        /// by setting the REDIS_URL environment variable and establishing a dependency on the Redis resource.
        /// </summary>
        /// <param name="redis">The resource builder for the Redis resource to be used by the Logto  resource.</param>
        /// <returns>The resource builder configured with the specified Redis resource.</returns>
        public IResourceBuilder<LogtoResource> WithRedis(IResourceBuilder<RedisResource> redis)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(redis);

            return builder.WithEnvironment("REDIS_URL", redis.Resource.UriExpression)
                .WaitFor(redis);
        }

        /// <summary>
        /// Configures the Logto  resource to connect to the specified PostgreSQL database
        /// by setting the appropriate environment variables and establishing a dependency on the database resource.
        /// </summary>
        /// <param name="postgres">The resource builder for the PostgreSQL server to connect to.</param>
        /// <returns>The resource builder for the configured Logto  resource.</returns>
        public IResourceBuilder<LogtoResource> WithDatabase(IResourceBuilder<PostgresServerResource> postgres)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(postgres);

            var dbUrlBuilder = new ReferenceExpressionBuilder();
            //I don't why actually db must be logto_db
            dbUrlBuilder.Append($"{postgres!.Resource.UriExpression}/logto_db");
            var dbUrl = dbUrlBuilder.Build();


            builder.WithEnvironment("DB_URL", dbUrl)
                .WithDataVolume("logto-data")
                .WaitFor(postgres);
            return builder;
        }
    }
}
