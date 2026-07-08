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

        return AddPostaCore(builder, name, jwtSecret, adminPassword, adminEmail, port, options: null)
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
    /// <param name="options">The parameter-based Posta environment configuration.</param>
    /// <param name="jwtSecret">Optional parameter used as the Posta JWT signing secret.</param>
    /// <param name="adminPassword">Optional parameter used as the initial Posta admin password.</param>
    /// <param name="adminEmail">The initial Posta admin account email.</param>
    /// <param name="port">Optional host port for the Posta HTTP API and dashboard.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostaResource}"/> for further resource configuration.</returns>
    [AspireExportIgnore(Reason = "PostaOptions contains parameter builders and is not supported in polyglot app hosts.")]
    public static IResourceBuilder<PostaResource> AddPosta(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<PostgresDatabaseResource> database,
        IResourceBuilder<RedisResource> redis,
        PostaOptions options,
        IResourceBuilder<ParameterResource>? jwtSecret = null,
        IResourceBuilder<ParameterResource>? adminPassword = null,
        string adminEmail = "admin@example.com",
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);

        return AddPostaCore(builder, name, jwtSecret, adminPassword, adminEmail, port, options)
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
    [AspireExportIgnore(Reason = "Action<PostaOptions> is not supported in polyglot app hosts. Use the options object overload instead.")]
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

        var options = new PostaOptions();
        configureOptions.Invoke(options);

        return builder.AddPosta(name, database, redis, options, jwtSecret, adminPassword, adminEmail, port);
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

                SetIfAbsent(context, "POSTA_REDIS_ADDR", ReferenceExpression.Create($"{redisEndpoint.Property(EndpointProperty.Host)}:{redisEndpoint.Property(EndpointProperty.Port)}"));
                if (redisPassword is not null)
                {
                    Set(context, "POSTA_REDIS_PASSWORD", redisPassword.Resource);
                }
                else if (redisResource.PasswordParameter is not null)
                {
                    SetIfAbsent(context, "POSTA_REDIS_PASSWORD", redisResource.PasswordParameter);
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
        PostaOptions? options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminEmail);

        options ??= new PostaOptions();

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
                SetParameter(context, "POSTA_REDIS_ADDR", options.RedisAddress);
                if (options.RedisPassword is not null)
                {
                    Set(context, "POSTA_REDIS_PASSWORD", options.RedisPassword.Resource);
                }
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
        SetParameter(context, "POSTA_ENV", options.Environment, "production");
        SetParameter(context, "POSTA_DEV_MODE", options.DevMode, "false");
        SetParameter(context, "POSTA_AUTH_RATE_LIMIT_ENABLED", options.AuthRateLimitEnabled, "true");
        SetParameter(context, "POSTA_RATE_LIMIT_HOURLY", options.RateLimitHourly, "100");
        SetParameter(context, "POSTA_RATE_LIMIT_DAILY", options.RateLimitDaily, "1000");
        SetParameter(context, "POSTA_OPENAPI_DOCS", options.OpenApiDocs, "true");
        SetParameter(context, "POSTA_METRICS_ENABLED", options.MetricsEnabled, "false");
        SetParameter(context, "POSTA_WEB_DIR", options.WebDir);
        SetParameter(context, "POSTA_WEB_URL", options.WebUrl);
        SetParameter(context, "POSTA_API_URL", options.ApiUrl);
        SetParameter(context, "POSTA_CORS_ORIGINS", options.CorsOrigins, "*");
        SetParameter(context, "POSTA_EMBEDDED_WORKER", options.EmbeddedWorker, "true");
        SetParameter(context, "POSTA_WORKER_CONCURRENCY", options.WorkerConcurrency, "10");
        SetParameter(context, "POSTA_WORKER_MAX_RETRIES", options.WorkerMaxRetries, "5");
        SetParameter(context, "POSTA_WEBHOOK_MAX_RETRIES", options.WebhookMaxRetries, "3");
        SetParameter(context, "POSTA_WEBHOOK_TIMEOUT_SECS", options.WebhookTimeoutSeconds, "10");
        SetParameter(context, "POSTA_WEBHOOK_PROXY_URL", options.WebhookProxyUrl);
        SetParameter(context, "POSTA_GOOGLE_OAUTH_CLIENT_ID", options.GoogleOAuthClientId);
        SetParameter(context, "POSTA_GOOGLE_OAUTH_CLIENT_SECRET", options.GoogleOAuthClientSecret);
        SetParameter(context, "POSTA_OAUTH_CALLBACK_URL", options.OAuthCallbackUrl);
        SetParameter(context, "POSTA_BLOB_PROVIDER", options.BlobProvider);
        SetParameter(context, "POSTA_BLOB_S3_ENDPOINT", options.BlobS3Endpoint);
        SetParameter(context, "POSTA_BLOB_S3_REGION", options.BlobS3Region, "us-east-1");
        SetParameter(context, "POSTA_BLOB_S3_BUCKET", options.BlobS3Bucket);
        SetParameter(context, "POSTA_BLOB_S3_ACCESS_KEY", options.BlobS3AccessKey);
        SetParameter(context, "POSTA_BLOB_S3_SECRET_KEY", options.BlobS3SecretKey);
        SetParameter(context, "POSTA_BLOB_S3_USE_SSL", options.BlobS3UseSsl, "true");
        SetParameter(context, "POSTA_BLOB_S3_PATH_STYLE", options.BlobS3PathStyle, "false");
        SetParameter(context, "POSTA_BLOB_FS_PATH", options.BlobFileSystemPath, "/data/attachments");
        SetParameter(context, "POSTA_ENCRYPTION_KEY", options.EncryptionKey);
        SetParameter(context, "POSTA_SYSTEM_SMTP_HOST", options.SystemSmtpHost);
        SetParameter(context, "POSTA_SYSTEM_SMTP_PORT", options.SystemSmtpPort, "587");
        SetParameter(context, "POSTA_SYSTEM_SMTP_USERNAME", options.SystemSmtpUsername);
        SetParameter(context, "POSTA_SYSTEM_SMTP_PASSWORD", options.SystemSmtpPassword);
        SetParameter(context, "POSTA_SYSTEM_SMTP_FROM", options.SystemSmtpFrom);
        SetParameter(context, "POSTA_SYSTEM_SMTP_ENCRYPTION", options.SystemSmtpEncryption, "starttls");
        SetParameter(context, "POSTA_INBOUND_ENABLED", options.InboundEnabled, "false");
        SetParameter(context, "POSTA_INBOUND_SMTP_HOST", options.InboundSmtpHost, "0.0.0.0");
        SetParameter(context, "POSTA_INBOUND_SMTP_PORT", options.InboundSmtpPort, "2525");
        SetParameter(context, "POSTA_INBOUND_HOSTNAME", options.InboundHostname, "posta.local");
        SetParameter(context, "POSTA_INBOUND_MAX_MESSAGE_SIZE", options.InboundMaxMessageSize, "26214400");
        SetParameter(context, "POSTA_INBOUND_MAX_ATTACH_SIZE", options.InboundMaxAttachSize, "10485760");
        SetParameter(context, "POSTA_INBOUND_WEBHOOK_SECRET", options.InboundWebhookSecret);
        SetParameter(context, "POSTA_INBOUND_TLS_MODE", options.InboundTlsMode, "none");
        SetParameter(context, "POSTA_INBOUND_TLS_CERT_FILE", options.InboundTlsCertFile);
        SetParameter(context, "POSTA_INBOUND_TLS_KEY_FILE", options.InboundTlsKeyFile);
        SetParameter(context, "POSTA_INBOUND_SMTP_RATE_LIMIT", options.InboundSmtpRateLimit, "60");
        SetParameter(context, "POSTA_INBOUND_SMTP_RATE_WINDOW", options.InboundSmtpRateWindow, "60");
        SetParameter(context, "POSTA_EMAIL_VERIFICATION_REQUIRED", options.EmailVerificationRequired, "false");
        SetParameter(context, "POSTA_AUTO_SUPPRESS_ON_REJECT", options.AutoSuppressOnReject, "true");
        SetParameter(context, "POSTA_EMAIL_VERIFY_ENABLED", options.EmailVerifyEnabled, "true");
        SetParameter(context, "POSTA_EMAIL_VERIFY_CACHE_TTL_HOURS", options.EmailVerifyCacheTtlHours, "168");
        SetParameter(context, "POSTA_EMAIL_VERIFY_MX_CACHE_TTL_HOURS", options.EmailVerifyMxCacheTtlHours, "24");
        SetParameter(context, "POSTA_EMAIL_VERIFY_RATE_HOURLY", options.EmailVerifyRateHourly, "1000");
        SetParameter(context, "POSTA_ALLOW_DOWNGRADE", options.AllowDowngrade, "false");
        SetParameter(context, "POSTA_PLAN_ENFORCEMENT", options.PlanEnforcement, "false");
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

    private static void SetIfAbsent(EnvironmentCallbackContext context, string name, ParameterResource value)
    {
        context.EnvironmentVariables.TryAdd(name, value);
    }

    private static void SetIfAbsent(EnvironmentCallbackContext context, string name, ReferenceExpression value)
    {
        context.EnvironmentVariables.TryAdd(name, value);
    }

    private static void Set(EnvironmentCallbackContext context, string name, int value)
    {
        context.EnvironmentVariables[name] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void SetParameter(EnvironmentCallbackContext context, string name, IResourceBuilder<ParameterResource>? parameter)
    {
        if (parameter is not null)
        {
            context.EnvironmentVariables[name] = parameter.Resource;
        }
    }

    private static void SetParameter(
        EnvironmentCallbackContext context,
        string name,
        IResourceBuilder<ParameterResource>? parameter,
        string defaultValue)
    {
        if (parameter is not null)
        {
            context.EnvironmentVariables[name] = parameter.Resource;
            return;
        }

        context.EnvironmentVariables[name] = defaultValue;
    }
}

