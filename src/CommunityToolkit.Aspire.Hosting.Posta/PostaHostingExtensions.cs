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
    /// <param name="options">The Posta environment configuration.</param>
    /// <param name="jwtSecret">Optional parameter used as the Posta JWT signing secret.</param>
    /// <param name="adminPassword">Optional parameter used as the initial Posta admin password.</param>
    /// <param name="adminEmail">The initial Posta admin account email.</param>
    /// <param name="port">Optional host port for the Posta HTTP API and dashboard.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{PostaResource}"/> for further resource configuration.</returns>
    [AspireExportIgnore(Reason = "PostaOptions contains parameter builders for secret values and is not supported in polyglot app hosts.")]
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

                Set(context, PostaEnvironmentVariables.DbHost, postgres.PrimaryEndpoint.Property(EndpointProperty.Host));
                Set(context, PostaEnvironmentVariables.DbPort, postgres.PrimaryEndpoint.Property(EndpointProperty.Port));
                Set(context, PostaEnvironmentVariables.DbUser, postgres.UserNameReference);
                Set(context, PostaEnvironmentVariables.DbPassword, postgres.PasswordParameter);
                Set(context, PostaEnvironmentVariables.DbName, database.Resource.DatabaseName);
                Set(context, PostaEnvironmentVariables.DbSslMode, "disable");
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

                SetIfAbsent(context, PostaEnvironmentVariables.RedisAddress, ReferenceExpression.Create($"{redisEndpoint.Property(EndpointProperty.Host)}:{redisEndpoint.Property(EndpointProperty.Port)}"));
                if (redisPassword is not null)
                {
                    Set(context, PostaEnvironmentVariables.RedisPassword, redisPassword.Resource);
                }
                else if (redisResource.PasswordParameter is not null)
                {
                    SetIfAbsent(context, PostaEnvironmentVariables.RedisPassword, redisResource.PasswordParameter);
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
                Set(context, PostaEnvironmentVariables.Port, PostaResource.HttpEndpointPort);
                SetParameter(context, PostaEnvironmentVariables.DbUrl, options.DatabaseUrl);
                SetIfNotNull(context, PostaEnvironmentVariables.RedisAddress, options.RedisAddress);
                if (options.RedisPassword is not null)
                {
                    Set(context, PostaEnvironmentVariables.RedisPassword, options.RedisPassword.Resource);
                }
                Set(context, PostaEnvironmentVariables.JwtSecret, resource.JwtSecretParameter);
                Set(context, PostaEnvironmentVariables.AdminEmail, adminEmail);
                Set(context, PostaEnvironmentVariables.AdminPassword, resource.AdminPasswordParameter);
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
        Set(context, PostaEnvironmentVariables.Environment, options.Environment);
        Set(context, PostaEnvironmentVariables.DevMode, options.DevMode);
        Set(context, PostaEnvironmentVariables.AuthRateLimitEnabled, options.AuthRateLimitEnabled);
        Set(context, PostaEnvironmentVariables.RateLimitHourly, options.RateLimitHourly);
        Set(context, PostaEnvironmentVariables.RateLimitDaily, options.RateLimitDaily);
        Set(context, PostaEnvironmentVariables.OpenApiDocs, options.OpenApiDocs);
        Set(context, PostaEnvironmentVariables.MetricsEnabled, options.MetricsEnabled);
        SetIfNotNull(context, PostaEnvironmentVariables.WebDir, options.WebDir);
        SetIfNotNull(context, PostaEnvironmentVariables.WebUrl, options.WebUrl);
        SetIfNotNull(context, PostaEnvironmentVariables.ApiUrl, options.ApiUrl);
        Set(context, PostaEnvironmentVariables.CorsOrigins, options.CorsOrigins);
        Set(context, PostaEnvironmentVariables.EmbeddedWorker, options.EmbeddedWorker);
        Set(context, PostaEnvironmentVariables.WorkerConcurrency, options.WorkerConcurrency);
        Set(context, PostaEnvironmentVariables.WorkerMaxRetries, options.WorkerMaxRetries);
        Set(context, PostaEnvironmentVariables.WebhookMaxRetries, options.WebhookMaxRetries);
        Set(context, PostaEnvironmentVariables.WebhookTimeoutSeconds, options.WebhookTimeoutSeconds);
        SetIfNotNull(context, PostaEnvironmentVariables.WebhookProxyUrl, options.WebhookProxyUrl);
        ConfigureGoogleOAuth(context, options);
        ConfigureBlobStorage(context, options);
        SetParameter(context, PostaEnvironmentVariables.EncryptionKey, options.EncryptionKey);
        ConfigureSystemSmtp(context, options);
        ConfigureInboundSmtp(context, options);
        ConfigureEmailVerification(context, options);
        Set(context, PostaEnvironmentVariables.AllowDowngrade, options.AllowDowngrade);
        Set(context, PostaEnvironmentVariables.PlanEnforcement, options.PlanEnforcement);
    }

    private static void ConfigureGoogleOAuth(EnvironmentCallbackContext context, PostaOptions options)
    {
        SetIfNotNull(context, PostaEnvironmentVariables.GoogleOAuthClientId, options.GoogleOAuthClientId);
        SetParameter(context, PostaEnvironmentVariables.GoogleOAuthClientSecret, options.GoogleOAuthClientSecret);
        SetIfNotNull(context, PostaEnvironmentVariables.OAuthCallbackUrl, options.OAuthCallbackUrl);
    }

    private static void ConfigureGoogleOAuth(EnvironmentCallbackContext context, PostaGoogleOAuthOptions options)
    {
        SetParameter(context, PostaEnvironmentVariables.GoogleOAuthClientId, options.ClientId);
        SetParameter(context, PostaEnvironmentVariables.GoogleOAuthClientSecret, options.ClientSecret);
        SetParameter(context, PostaEnvironmentVariables.OAuthCallbackUrl, options.CallbackUrl);
    }

    private static void ConfigureBlobStorage(EnvironmentCallbackContext context, PostaOptions options)
    {
        SetIfNotNull(context, PostaEnvironmentVariables.BlobProvider, options.BlobProvider);
        SetIfNotNull(context, PostaEnvironmentVariables.BlobS3Endpoint, options.BlobS3Endpoint);
        Set(context, PostaEnvironmentVariables.BlobS3Region, options.BlobS3Region);
        SetIfNotNull(context, PostaEnvironmentVariables.BlobS3Bucket, options.BlobS3Bucket);
        SetParameter(context, PostaEnvironmentVariables.BlobS3AccessKey, options.BlobS3AccessKey);
        SetParameter(context, PostaEnvironmentVariables.BlobS3SecretKey, options.BlobS3SecretKey);
        Set(context, PostaEnvironmentVariables.BlobS3UseSsl, options.BlobS3UseSsl);
        Set(context, PostaEnvironmentVariables.BlobS3PathStyle, options.BlobS3PathStyle);
        Set(context, PostaEnvironmentVariables.BlobFileSystemPath, options.BlobFileSystemPath);
    }

    private static void ConfigureS3BlobStorage(EnvironmentCallbackContext context, PostaS3BlobStorageOptions options)
    {
        Set(context, PostaEnvironmentVariables.BlobProvider, "s3");
        SetParameter(context, PostaEnvironmentVariables.BlobS3Endpoint, options.Endpoint);
        SetParameter(context, PostaEnvironmentVariables.BlobS3Region, options.Region);
        SetParameter(context, PostaEnvironmentVariables.BlobS3Bucket, options.Bucket);
        SetParameter(context, PostaEnvironmentVariables.BlobS3AccessKey, options.AccessKey);
        SetParameter(context, PostaEnvironmentVariables.BlobS3SecretKey, options.SecretKey);
        SetParameter(context, PostaEnvironmentVariables.BlobS3UseSsl, options.UseSsl);
        SetParameter(context, PostaEnvironmentVariables.BlobS3PathStyle, options.PathStyle);
    }

    private static void ConfigureSystemSmtp(EnvironmentCallbackContext context, PostaOptions options)
    {
        SetIfNotNull(context, PostaEnvironmentVariables.SystemSmtpHost, options.SystemSmtpHost);
        Set(context, PostaEnvironmentVariables.SystemSmtpPort, options.SystemSmtpPort);
        SetIfNotNull(context, PostaEnvironmentVariables.SystemSmtpUsername, options.SystemSmtpUsername);
        SetParameter(context, PostaEnvironmentVariables.SystemSmtpPassword, options.SystemSmtpPassword);
        SetIfNotNull(context, PostaEnvironmentVariables.SystemSmtpFrom, options.SystemSmtpFrom);
        Set(context, PostaEnvironmentVariables.SystemSmtpEncryption, options.SystemSmtpEncryption);
    }

    private static void ConfigureSystemSmtp(EnvironmentCallbackContext context, PostaSystemSmtpOptions options)
    {
        SetParameter(context, PostaEnvironmentVariables.SystemSmtpHost, options.Host);
        SetParameter(context, PostaEnvironmentVariables.SystemSmtpPort, options.Port);
        SetParameter(context, PostaEnvironmentVariables.SystemSmtpUsername, options.Username);
        SetParameter(context, PostaEnvironmentVariables.SystemSmtpPassword, options.Password);
        SetParameter(context, PostaEnvironmentVariables.SystemSmtpFrom, options.From);
        SetParameter(context, PostaEnvironmentVariables.SystemSmtpEncryption, options.Encryption);
    }

    private static void ConfigureInboundSmtp(EnvironmentCallbackContext context, PostaOptions options)
    {
        Set(context, PostaEnvironmentVariables.InboundEnabled, options.InboundEnabled);
        Set(context, PostaEnvironmentVariables.InboundSmtpHost, options.InboundSmtpHost);
        Set(context, PostaEnvironmentVariables.InboundSmtpPort, options.InboundSmtpPort);
        Set(context, PostaEnvironmentVariables.InboundHostname, options.InboundHostname);
        Set(context, PostaEnvironmentVariables.InboundMaxMessageSize, options.InboundMaxMessageSize);
        Set(context, PostaEnvironmentVariables.InboundMaxAttachSize, options.InboundMaxAttachSize);
        SetParameter(context, PostaEnvironmentVariables.InboundWebhookSecret, options.InboundWebhookSecret);
        Set(context, PostaEnvironmentVariables.InboundTlsMode, options.InboundTlsMode);
        SetIfNotNull(context, PostaEnvironmentVariables.InboundTlsCertFile, options.InboundTlsCertFile);
        SetIfNotNull(context, PostaEnvironmentVariables.InboundTlsKeyFile, options.InboundTlsKeyFile);
        Set(context, PostaEnvironmentVariables.InboundSmtpRateLimit, options.InboundSmtpRateLimit);
        Set(context, PostaEnvironmentVariables.InboundSmtpRateWindow, options.InboundSmtpRateWindow);
    }

    private static void ConfigureInboundSmtp(EnvironmentCallbackContext context, PostaInboundSmtpOptions options)
    {
        SetParameter(context, PostaEnvironmentVariables.InboundEnabled, options.Enabled);
        SetParameter(context, PostaEnvironmentVariables.InboundSmtpHost, options.Host);
        SetParameter(context, PostaEnvironmentVariables.InboundSmtpPort, options.Port);
        SetParameter(context, PostaEnvironmentVariables.InboundHostname, options.Hostname);
        SetParameter(context, PostaEnvironmentVariables.InboundMaxMessageSize, options.MaxMessageSize);
        SetParameter(context, PostaEnvironmentVariables.InboundMaxAttachSize, options.MaxAttachmentSize);
        SetParameter(context, PostaEnvironmentVariables.InboundWebhookSecret, options.WebhookSecret);
        SetParameter(context, PostaEnvironmentVariables.InboundTlsMode, options.TlsMode);
        SetParameter(context, PostaEnvironmentVariables.InboundTlsCertFile, options.TlsCertFile);
        SetParameter(context, PostaEnvironmentVariables.InboundTlsKeyFile, options.TlsKeyFile);
        SetParameter(context, PostaEnvironmentVariables.InboundSmtpRateLimit, options.RateLimit);
        SetParameter(context, PostaEnvironmentVariables.InboundSmtpRateWindow, options.RateWindow);
    }

    private static void ConfigureEmailVerification(EnvironmentCallbackContext context, PostaOptions options)
    {
        Set(context, PostaEnvironmentVariables.EmailVerificationRequired, options.EmailVerificationRequired);
        Set(context, PostaEnvironmentVariables.AutoSuppressOnReject, options.AutoSuppressOnReject);
        Set(context, PostaEnvironmentVariables.EmailVerifyEnabled, options.EmailVerifyEnabled);
        Set(context, PostaEnvironmentVariables.EmailVerifyCacheTtlHours, options.EmailVerifyCacheTtlHours);
        Set(context, PostaEnvironmentVariables.EmailVerifyMxCacheTtlHours, options.EmailVerifyMxCacheTtlHours);
        Set(context, PostaEnvironmentVariables.EmailVerifyRateHourly, options.EmailVerifyRateHourly);
    }

    private static void ConfigureEmailVerification(EnvironmentCallbackContext context, PostaEmailVerificationOptions options)
    {
        SetParameter(context, PostaEnvironmentVariables.EmailVerificationRequired, options.Required);
        SetParameter(context, PostaEnvironmentVariables.AutoSuppressOnReject, options.AutoSuppressOnReject);
        SetParameter(context, PostaEnvironmentVariables.EmailVerifyEnabled, options.Enabled);
        SetParameter(context, PostaEnvironmentVariables.EmailVerifyCacheTtlHours, options.CacheTtlHours);
        SetParameter(context, PostaEnvironmentVariables.EmailVerifyMxCacheTtlHours, options.MxCacheTtlHours);
        SetParameter(context, PostaEnvironmentVariables.EmailVerifyRateHourly, options.RateHourly);
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
