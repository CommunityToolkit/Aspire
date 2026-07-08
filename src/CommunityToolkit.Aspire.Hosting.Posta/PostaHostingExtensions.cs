using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Posta;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Posta to an <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class PostaHostingExtensions
{
    /// <summary>
    /// Adds a Posta container resource to the <see cref="IDistributedApplicationBuilder"/> and configures PostgreSQL and Redis references.
    /// </summary>
    /// <ats-summary>Adds a Posta container resource with PostgreSQL and Redis references</ats-summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the Posta resource will be added.</param>
    /// <param name="name">The name of the Posta container resource.</param>
    /// <param name="database">The PostgreSQL database resource used by Posta.</param>
    /// <param name="redis">The Redis resource used by Posta.</param>
    /// <param name="jwtSecret">Optional parameter used as the Posta JWT signing secret.</param>
    /// <param name="adminPassword">Optional parameter used as the initial Posta admin password.</param>
    /// <param name="adminEmail">The initial Posta admin account email.</param>
    /// <param name="port">Optional host port for the Posta HTTP API and dashboard.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostaResource}"/> for further resource configuration.</returns>
    [AspireExport("addPostaWithReferences", MethodName = "addPostaWithReferences")]
    public static IResourceBuilder<PostaResource> AddPosta(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<PostgresDatabaseResource> database,
        IResourceBuilder<RedisResource> redis,
        IResourceBuilder<ParameterResource>? jwtSecret = null,
        IResourceBuilder<ParameterResource>? adminPassword = null,
        string adminEmail = "admin@example.com",
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(redis);

        return AddPostaCore(builder, name, jwtSecret, adminPassword, adminEmail, port, configureOptions: null)
            .WithReference(database)
            .WithReference(redis);
    }

    /// <summary>
    /// Adds a Posta container resource to the <see cref="IDistributedApplicationBuilder"/> with PostgreSQL, Redis, and additional Posta environment configuration.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to which the Posta resource will be added.</param>
    /// <param name="name">The name of the Posta container resource.</param>
    /// <param name="database">The PostgreSQL database resource used by Posta.</param>
    /// <param name="redis">The Redis resource used by Posta.</param>
    /// <param name="configureOptions">A delegate that configures Posta environment variables.</param>
    /// <param name="jwtSecret">Optional parameter used as the Posta JWT signing secret.</param>
    /// <param name="adminPassword">Optional parameter used as the initial Posta admin password.</param>
    /// <param name="adminEmail">The initial Posta admin account email.</param>
    /// <param name="port">Optional host port for the Posta HTTP API and dashboard.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostaResource}"/> for further resource configuration.</returns>
    [AspireExportIgnore(Reason = "Action<PostaOptions> is not supported in polyglot app hosts. Use the parameter-based overload instead.")]
    public static IResourceBuilder<PostaResource> AddPosta(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<PostgresDatabaseResource> database,
        IResourceBuilder<RedisResource> redis,
        Action<PostaOptions> configureOptions,
        IResourceBuilder<ParameterResource>? jwtSecret = null,
        IResourceBuilder<ParameterResource>? adminPassword = null,
        string adminEmail = "admin@example.com",
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return AddPostaCore(builder, name, jwtSecret, adminPassword, adminEmail, port, configureOptions)
            .WithReference(database)
            .WithReference(redis);
    }

    /// <summary>
    /// Configures the PostgreSQL database used by Posta.
    /// </summary>
    /// <param name="builder">The Posta resource builder.</param>
    /// <param name="database">The PostgreSQL database resource used by Posta.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostaResource}"/> for further resource configuration.</returns>
    [AspireExport("withPostgresReference", MethodName = "withPostgresReference")]
    public static IResourceBuilder<PostaResource> WithReference(
        this IResourceBuilder<PostaResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        return builder
            .WithEnvironment(context =>
            {
                var postgres = database.Resource.Parent;

                Set(context, "POSTA_DB_HOST", postgres.PrimaryEndpoint.Property(EndpointProperty.Host));
                Set(context, "POSTA_DB_PORT", postgres.PrimaryEndpoint.Property(EndpointProperty.Port));
                Set(context, "POSTA_DB_USER", postgres.UserNameReference);
                Set(context, "POSTA_DB_PASSWORD", postgres.PasswordParameter);
                Set(context, "POSTA_DB_NAME", database.Resource.DatabaseName);
                Set(context, "POSTA_DB_SSL_MODE", "disable");
            })
            .WaitFor(database);
    }

    /// <summary>
    /// Configures the Redis server used by Posta.
    /// </summary>
    /// <param name="builder">The Posta resource builder.</param>
    /// <param name="redis">The Redis resource used by Posta.</param>
    /// <param name="redisPassword">Optional Redis password parameter that overrides the referenced Redis resource password.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostaResource}"/> for further resource configuration.</returns>
    [AspireExport("withRedisReference", MethodName = "withRedisReference")]
    public static IResourceBuilder<PostaResource> WithReference(
        this IResourceBuilder<PostaResource> builder,
        IResourceBuilder<RedisResource> redis,
        IResourceBuilder<ParameterResource>? redisPassword = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(redis);

        return builder
            .WithEnvironment(context =>
            {
                var redisResource = redis.Resource;
                var redisEndpoint = redisResource.GetEndpoint("secondary");

                Set(context, "POSTA_REDIS_ADDR", ReferenceExpression.Create($"{redisEndpoint.Property(EndpointProperty.Host)}:{redisEndpoint.Property(EndpointProperty.Port)}"));
                if (redisPassword is not null)
                {
                    Set(context, "POSTA_REDIS_PASSWORD", redisPassword.Resource);
                }
                else if (redisResource.PasswordParameter is not null)
                {
                    Set(context, "POSTA_REDIS_PASSWORD", redisResource.PasswordParameter);
                }
            })
            .WaitFor(redis);
    }

    private static IResourceBuilder<PostaResource> AddPostaCore(
        IDistributedApplicationBuilder builder,
        string name,
        IResourceBuilder<ParameterResource>? jwtSecret,
        IResourceBuilder<ParameterResource>? adminPassword,
        string adminEmail,
        int? port,
        Action<PostaOptions>? configureOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminEmail);

        var options = new PostaOptions();
        configureOptions?.Invoke(options);

        var jwtSecretParameter = jwtSecret?.Resource ??
            ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-jwt-secret", minLower: 1, minUpper: 1, minNumeric: 1);
        var adminPasswordParameter = adminPassword?.Resource ??
            ParameterResourceBuilderExtensions.CreateDefaultPasswordParameter(builder, $"{name}-admin-password", minLower: 1, minUpper: 1, minNumeric: 1);

        var resource = new PostaResource(name, jwtSecretParameter, adminPasswordParameter);

        return builder.AddResource(resource)
            .WithImage(PostaContainerImageTags.Image)
            .WithImageTag(PostaContainerImageTags.Tag)
            .WithImageRegistry(PostaContainerImageTags.Registry)
            .WithHttpEndpoint(
                targetPort: PostaResource.HttpEndpointPort,
                port: port,
                name: PostaResource.HttpEndpointName)
            .WithEnvironment(context =>
            {
                Set(context, "POSTA_PORT", PostaResource.HttpEndpointPort);
                SetParameter(context, "POSTA_DB_URL", options.DatabaseUrl);
                SetIfNotNull(context, "POSTA_REDIS_ADDR", options.RedisAddress);
                if (options.RedisPassword is not null)
                Set(context, "POSTA_JWT_SECRET", resource.JwtSecretParameter);
                Set(context, "POSTA_ADMIN_EMAIL", adminEmail);
                Set(context, "POSTA_ADMIN_PASSWORD", resource.AdminPasswordParameter);
                ConfigurePostaEnvironment(context, options);
            })
            .WithHttpHealthCheck(
                path: "/healthz",
                statusCode: 200,
                endpointName: PostaResource.HttpEndpointName)
            .WithHttpHealthCheck(
                path: "/readyz",
                statusCode: 200,
                endpointName: PostaResource.HttpEndpointName)
            .WithUrlForEndpoint(PostaResource.HttpEndpointName, url => url.DisplayText = "Posta Dashboard");
    }

    private static void ConfigurePostaEnvironment(EnvironmentCallbackContext context, PostaOptions options)
    {
        Set(context, "POSTA_ENV", options.Environment);
        Set(context, "POSTA_DEV_MODE", options.DevMode);
        Set(context, "POSTA_AUTH_RATE_LIMIT_ENABLED", options.AuthRateLimitEnabled);
        Set(context, "POSTA_RATE_LIMIT_HOURLY", options.RateLimitHourly);
        Set(context, "POSTA_RATE_LIMIT_DAILY", options.RateLimitDaily);
        Set(context, "POSTA_OPENAPI_DOCS", options.OpenApiDocs);
        Set(context, "POSTA_METRICS_ENABLED", options.MetricsEnabled);
        SetIfNotNull(context, "POSTA_WEB_DIR", options.WebDir);
        SetIfNotNull(context, "POSTA_WEB_URL", options.WebUrl);
        SetIfNotNull(context, "POSTA_API_URL", options.ApiUrl);
        Set(context, "POSTA_CORS_ORIGINS", options.CorsOrigins);
        Set(context, "POSTA_EMBEDDED_WORKER", options.EmbeddedWorker);
        Set(context, "POSTA_WORKER_CONCURRENCY", options.WorkerConcurrency);
        Set(context, "POSTA_WORKER_MAX_RETRIES", options.WorkerMaxRetries);
        Set(context, "POSTA_WEBHOOK_MAX_RETRIES", options.WebhookMaxRetries);
        Set(context, "POSTA_WEBHOOK_TIMEOUT_SECS", options.WebhookTimeoutSeconds);
        SetIfNotNull(context, "POSTA_WEBHOOK_PROXY_URL", options.WebhookProxyUrl);
        SetIfNotNull(context, "POSTA_GOOGLE_OAUTH_CLIENT_ID", options.GoogleOAuthClientId);
        SetParameter(context, "POSTA_GOOGLE_OAUTH_CLIENT_SECRET", options.GoogleOAuthClientSecret);
        SetIfNotNull(context, "POSTA_OAUTH_CALLBACK_URL", options.OAuthCallbackUrl);
        SetIfNotNull(context, "POSTA_BLOB_PROVIDER", options.BlobProvider);
        SetIfNotNull(context, "POSTA_BLOB_S3_ENDPOINT", options.BlobS3Endpoint);
        Set(context, "POSTA_BLOB_S3_REGION", options.BlobS3Region);
        SetIfNotNull(context, "POSTA_BLOB_S3_BUCKET", options.BlobS3Bucket);
        SetParameter(context, "POSTA_BLOB_S3_ACCESS_KEY", options.BlobS3AccessKey);
        SetParameter(context, "POSTA_BLOB_S3_SECRET_KEY", options.BlobS3SecretKey);
        Set(context, "POSTA_BLOB_S3_USE_SSL", options.BlobS3UseSsl);
        Set(context, "POSTA_BLOB_S3_PATH_STYLE", options.BlobS3PathStyle);
        Set(context, "POSTA_BLOB_FS_PATH", options.BlobFileSystemPath);
        SetParameter(context, "POSTA_ENCRYPTION_KEY", options.EncryptionKey);
        SetIfNotNull(context, "POSTA_SYSTEM_SMTP_HOST", options.SystemSmtpHost);
        Set(context, "POSTA_SYSTEM_SMTP_PORT", options.SystemSmtpPort);
        SetIfNotNull(context, "POSTA_SYSTEM_SMTP_USERNAME", options.SystemSmtpUsername);
        SetParameter(context, "POSTA_SYSTEM_SMTP_PASSWORD", options.SystemSmtpPassword);
        SetIfNotNull(context, "POSTA_SYSTEM_SMTP_FROM", options.SystemSmtpFrom);
        Set(context, "POSTA_SYSTEM_SMTP_ENCRYPTION", options.SystemSmtpEncryption);
        Set(context, "POSTA_INBOUND_ENABLED", options.InboundEnabled);
        Set(context, "POSTA_INBOUND_SMTP_HOST", options.InboundSmtpHost);
        Set(context, "POSTA_INBOUND_SMTP_PORT", options.InboundSmtpPort);
        Set(context, "POSTA_INBOUND_HOSTNAME", options.InboundHostname);
        Set(context, "POSTA_INBOUND_MAX_MESSAGE_SIZE", options.InboundMaxMessageSize);
        Set(context, "POSTA_INBOUND_MAX_ATTACH_SIZE", options.InboundMaxAttachSize);
        SetParameter(context, "POSTA_INBOUND_WEBHOOK_SECRET", options.InboundWebhookSecret);
        Set(context, "POSTA_INBOUND_TLS_MODE", options.InboundTlsMode);
        SetIfNotNull(context, "POSTA_INBOUND_TLS_CERT_FILE", options.InboundTlsCertFile);
        SetIfNotNull(context, "POSTA_INBOUND_TLS_KEY_FILE", options.InboundTlsKeyFile);
        Set(context, "POSTA_INBOUND_SMTP_RATE_LIMIT", options.InboundSmtpRateLimit);
        Set(context, "POSTA_INBOUND_SMTP_RATE_WINDOW", options.InboundSmtpRateWindow);
        Set(context, "POSTA_EMAIL_VERIFICATION_REQUIRED", options.EmailVerificationRequired);
        Set(context, "POSTA_AUTO_SUPPRESS_ON_REJECT", options.AutoSuppressOnReject);
        Set(context, "POSTA_EMAIL_VERIFY_ENABLED", options.EmailVerifyEnabled);
        Set(context, "POSTA_EMAIL_VERIFY_CACHE_TTL_HOURS", options.EmailVerifyCacheTtlHours);
        Set(context, "POSTA_EMAIL_VERIFY_MX_CACHE_TTL_HOURS", options.EmailVerifyMxCacheTtlHours);
        Set(context, "POSTA_EMAIL_VERIFY_RATE_HOURLY", options.EmailVerifyRateHourly);
        Set(context, "POSTA_ALLOW_DOWNGRADE", options.AllowDowngrade);
        Set(context, "POSTA_PLAN_ENFORCEMENT", options.PlanEnforcement);
    }

    private static void Set(EnvironmentCallbackContext context, string name, string value)
    {
        context.EnvironmentVariables[name] = value;
    }

    private static void Set(EnvironmentCallbackContext context, string name, ParameterResource value)
    {
        context.EnvironmentVariables[name] = value;
    }

    private static void Set(EnvironmentCallbackContext context, string name, EndpointReferenceExpression value)
    {
        context.EnvironmentVariables[name] = value;
    }

    private static void Set(EnvironmentCallbackContext context, string name, ReferenceExpression value)
    {
        context.EnvironmentVariables[name] = value;
    }

    private static void Set(EnvironmentCallbackContext context, string name, bool value)
    {
        context.EnvironmentVariables[name] = value ? "true" : "false";
    }

    private static void Set(EnvironmentCallbackContext context, string name, int value)
    {
        context.EnvironmentVariables[name] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void SetIfNotNull(EnvironmentCallbackContext context, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            context.EnvironmentVariables[name] = value;
        }
    }

    private static void SetParameter(EnvironmentCallbackContext context, string name, IResourceBuilder<ParameterResource>? parameter)
    {
        if (parameter is not null)
        {
            context.EnvironmentVariables[name] = parameter.Resource;
        }
    }
}

