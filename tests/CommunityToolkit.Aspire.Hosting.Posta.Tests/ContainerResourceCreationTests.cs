using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Posta.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddPostaThrowsWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => builder.AddPosta("posta", null!, null!));
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddPostaThrowsWhenNameIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);

        Assert.Throws<ArgumentNullException>(() => builder.AddPosta(null!, database, redis));
    }

    [Fact]
    public void WithReferenceThrowsWhenDatabaseIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);
        var posta = builder.AddPosta("posta", database, redis);

        Assert.Throws<ArgumentNullException>(() => posta.WithReference((IResourceBuilder<PostgresDatabaseResource>)null!));
    }

    [Fact]
    public void WithReferenceThrowsWhenRedisIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);
        var posta = builder.AddPosta("posta", database, redis);

        Assert.Throws<ArgumentNullException>(() => posta.WithReference((IResourceBuilder<RedisResource>)null!));
    }

    [Fact]
    public void AddPostaWithReferencesThrowsWhenDatabaseIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var redis = builder.AddRedis("redis");

        Assert.Throws<ArgumentNullException>(() => builder.AddPosta("posta", null!, redis));
    }

    [Fact]
    public void AddPostaWithReferencesThrowsWhenRedisIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");
        var database = postgres.AddDatabase("posta-db", "posta");

        Assert.Throws<ArgumentNullException>(() => builder.AddPosta("posta", database, null!));
    }

    [Fact]
    public void AddPostaSetsContainerDetailsOnResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);

        builder.AddPosta("posta", database, redis);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = Assert.Single(appModel.Resources.OfType<PostaResource>());

        Assert.Equal("posta", resource.Name);
        Assert.True(resource.TryGetLastAnnotation(out ContainerImageAnnotation? imageAnnotations));
        Assert.Equal(PostaContainerImageTags.Tag, imageAnnotations.Tag);
        Assert.NotEqual("latest", imageAnnotations.Tag);
        Assert.Equal(PostaContainerImageTags.Image, imageAnnotations.Image);
        Assert.Equal(PostaContainerImageTags.Registry, imageAnnotations.Registry);
    }

    [Fact]
    public void AddPostaSetsEndpointDetailsOnResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);

        builder.AddPosta("posta", database, redis, port: 9001);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PostaResource>());

        var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>(), x => x.Name == PostaResource.HttpEndpointName);
        Assert.Equal("http", endpoint.UriScheme);
        Assert.Equal(PostaResource.HttpEndpointPort, endpoint.TargetPort);
        Assert.Equal(9001, endpoint.Port);
    }

    [Fact]
    public void AddPostaRegistersHealthChecks()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);

        builder.AddPosta("posta", database, redis);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PostaResource>());

        Assert.True(resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var annotations));
        Assert.Equal(2, annotations.Count());
    }

    [Fact]
    public async Task AddPostaWithReferencesConfiguresPostgreSqlAndRedis()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");
        var database = postgres.AddDatabase("posta-db", "posta");
        var redis = builder.AddRedis("redis");

        var posta = builder.AddPosta("posta", database, redis);

        Assert.True(posta.Resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var annotations));

        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(
                new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));

        foreach (var annotation in annotations)
        {
            await annotation.Callback(context);
        }

        var env = context.EnvironmentVariables;

        Assert.True(env.ContainsKey("POSTA_DB_HOST"));
        Assert.True(env.ContainsKey("POSTA_DB_PORT"));
        Assert.True(env.ContainsKey("POSTA_DB_USER"));
        Assert.True(env.ContainsKey("POSTA_DB_PASSWORD"));
        Assert.Equal("posta", env["POSTA_DB_NAME"]);
        Assert.Equal("disable", env["POSTA_DB_SSL_MODE"]);
        Assert.True(env.ContainsKey("POSTA_REDIS_ADDR"));
    }

    [Fact]
    public async Task AddPostaConfiguresPostaEnvironmentVariables()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");
        var database = postgres.AddDatabase("posta-db", "posta");
        var redis = builder.AddRedis("redis");
        var parameters = new Dictionary<string, IResourceBuilder<ParameterResource>>();

        IResourceBuilder<ParameterResource> Parameter(string name, string value)
        {
            var parameter = builder.AddParameter($"posta-{name}", value);
            parameters[name] = parameter;
            return parameter;
        }

        var posta = builder.AddPosta("posta", database, redis, options =>
        {
            options.Environment = Parameter("environment", "dev");
            options.DevMode = Parameter("dev-mode", "true");
            options.AuthRateLimitEnabled = Parameter("auth-rate-limit-enabled", "false");
            options.RateLimitHourly = Parameter("rate-limit-hourly", "500");
            options.RateLimitDaily = Parameter("rate-limit-daily", "5000");
            options.OpenApiDocs = Parameter("openapi-docs", "false");
            options.MetricsEnabled = Parameter("metrics-enabled", "true");
            options.WebDir = Parameter("web-dir", "web/dist");
            options.WebUrl = Parameter("web-url", "https://posta.example.com");
            options.ApiUrl = Parameter("api-url", "https://api.posta.example.com");
            options.CorsOrigins = Parameter("cors-origins", "https://app.example.com");
            options.EmbeddedWorker = Parameter("embedded-worker", "false");
            options.WorkerConcurrency = Parameter("worker-concurrency", "20");
            options.WorkerMaxRetries = Parameter("worker-max-retries", "7");
            options.WebhookMaxRetries = Parameter("webhook-max-retries", "4");
            options.WebhookTimeoutSeconds = Parameter("webhook-timeout-seconds", "30");
            options.WebhookProxyUrl = Parameter("webhook-proxy-url", "http://proxy.example.com");
            options.GoogleOAuthClientId = Parameter("google-oauth-client-id", "google-client-id");
            options.GoogleOAuthClientSecret = Parameter("google-oauth-client-secret", "google-secret");
            options.OAuthCallbackUrl = Parameter("oauth-callback-url", "https://posta.example.com/oauth/callback");
            options.BlobProvider = Parameter("blob-provider", "s3");
            options.BlobS3Endpoint = Parameter("blob-s3-endpoint", "https://s3.example.com");
            options.BlobS3Region = Parameter("blob-s3-region", "eu-central-1");
            options.BlobS3Bucket = Parameter("blob-s3-bucket", "posta");
            options.BlobS3AccessKey = Parameter("blob-s3-access-key", "s3-access");
            options.BlobS3SecretKey = Parameter("blob-s3-secret-key", "s3-secret");
            options.BlobS3UseSsl = Parameter("blob-s3-use-ssl", "false");
            options.BlobS3PathStyle = Parameter("blob-s3-path-style", "true");
            options.BlobFileSystemPath = Parameter("blob-file-system-path", "/posta/attachments");
            options.EncryptionKey = Parameter("encryption-key", "encryption-secret");
            options.SystemSmtpHost = Parameter("system-smtp-host", "smtp.example.com");
            options.SystemSmtpPort = Parameter("system-smtp-port", "2525");
            options.SystemSmtpUsername = Parameter("system-smtp-username", "notifications@example.com");
            options.SystemSmtpPassword = Parameter("system-smtp-password", "smtp-secret");
            options.SystemSmtpFrom = Parameter("system-smtp-from", "notifications@example.com");
            options.SystemSmtpEncryption = Parameter("system-smtp-encryption", "ssl");
            options.InboundEnabled = Parameter("inbound-enabled", "true");
            options.InboundSmtpHost = Parameter("inbound-smtp-host", "127.0.0.1");
            options.InboundSmtpPort = Parameter("inbound-smtp-port", "2526");
            options.InboundHostname = Parameter("inbound-hostname", "mx.example.com");
            options.InboundMaxMessageSize = Parameter("inbound-max-message-size", "12345");
            options.InboundMaxAttachSize = Parameter("inbound-max-attach-size", "6789");
            options.InboundWebhookSecret = Parameter("inbound-webhook-secret", "inbound-secret");
            options.InboundTlsMode = Parameter("inbound-tls-mode", "starttls");
            options.InboundTlsCertFile = Parameter("inbound-tls-cert-file", "/certs/fullchain.pem");
            options.InboundTlsKeyFile = Parameter("inbound-tls-key-file", "/certs/privkey.pem");
            options.InboundSmtpRateLimit = Parameter("inbound-smtp-rate-limit", "10");
            options.InboundSmtpRateWindow = Parameter("inbound-smtp-rate-window", "20");
            options.EmailVerificationRequired = Parameter("email-verification-required", "true");
            options.AutoSuppressOnReject = Parameter("auto-suppress-on-reject", "false");
            options.EmailVerifyEnabled = Parameter("email-verify-enabled", "false");
            options.EmailVerifyCacheTtlHours = Parameter("email-verify-cache-ttl-hours", "12");
            options.EmailVerifyMxCacheTtlHours = Parameter("email-verify-mx-cache-ttl-hours", "6");
            options.EmailVerifyRateHourly = Parameter("email-verify-rate-hourly", "42");
            options.AllowDowngrade = Parameter("allow-downgrade", "true");
            options.PlanEnforcement = Parameter("plan-enforcement", "true");
            options.DatabaseUrl = Parameter("db-url", "postgres://example");
            options.RedisPassword = Parameter("redis-password", "redis-secret");
            options.RedisAddress = Parameter("redis-address", "redis.example.com:6379");
        })
            .WithReference(redis, parameters["redis-password"]);

        Assert.True(posta.Resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var annotations));

        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(
                new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));

        foreach (var annotation in annotations)
        {
            await annotation.Callback(context);
        }

        var env = context.EnvironmentVariables;

        Assert.Equal("9000", env["POSTA_PORT"]);
        foreach (var (parameterName, environmentVariableName) in new (string ParameterName, string EnvironmentVariableName)[]
        {
            ("environment", "POSTA_ENV"),
            ("dev-mode", "POSTA_DEV_MODE"),
            ("auth-rate-limit-enabled", "POSTA_AUTH_RATE_LIMIT_ENABLED"),
            ("rate-limit-hourly", "POSTA_RATE_LIMIT_HOURLY"),
            ("rate-limit-daily", "POSTA_RATE_LIMIT_DAILY"),
            ("openapi-docs", "POSTA_OPENAPI_DOCS"),
            ("metrics-enabled", "POSTA_METRICS_ENABLED"),
            ("web-dir", "POSTA_WEB_DIR"),
            ("web-url", "POSTA_WEB_URL"),
            ("api-url", "POSTA_API_URL"),
            ("cors-origins", "POSTA_CORS_ORIGINS"),
            ("embedded-worker", "POSTA_EMBEDDED_WORKER"),
            ("worker-concurrency", "POSTA_WORKER_CONCURRENCY"),
            ("worker-max-retries", "POSTA_WORKER_MAX_RETRIES"),
            ("webhook-max-retries", "POSTA_WEBHOOK_MAX_RETRIES"),
            ("webhook-timeout-seconds", "POSTA_WEBHOOK_TIMEOUT_SECS"),
            ("webhook-proxy-url", "POSTA_WEBHOOK_PROXY_URL"),
            ("google-oauth-client-id", "POSTA_GOOGLE_OAUTH_CLIENT_ID"),
            ("google-oauth-client-secret", "POSTA_GOOGLE_OAUTH_CLIENT_SECRET"),
            ("oauth-callback-url", "POSTA_OAUTH_CALLBACK_URL"),
            ("blob-provider", "POSTA_BLOB_PROVIDER"),
            ("blob-s3-endpoint", "POSTA_BLOB_S3_ENDPOINT"),
            ("blob-s3-region", "POSTA_BLOB_S3_REGION"),
            ("blob-s3-bucket", "POSTA_BLOB_S3_BUCKET"),
            ("blob-s3-access-key", "POSTA_BLOB_S3_ACCESS_KEY"),
            ("blob-s3-secret-key", "POSTA_BLOB_S3_SECRET_KEY"),
            ("blob-s3-use-ssl", "POSTA_BLOB_S3_USE_SSL"),
            ("blob-s3-path-style", "POSTA_BLOB_S3_PATH_STYLE"),
            ("blob-file-system-path", "POSTA_BLOB_FS_PATH"),
            ("encryption-key", "POSTA_ENCRYPTION_KEY"),
            ("system-smtp-host", "POSTA_SYSTEM_SMTP_HOST"),
            ("system-smtp-port", "POSTA_SYSTEM_SMTP_PORT"),
            ("system-smtp-username", "POSTA_SYSTEM_SMTP_USERNAME"),
            ("system-smtp-password", "POSTA_SYSTEM_SMTP_PASSWORD"),
            ("system-smtp-from", "POSTA_SYSTEM_SMTP_FROM"),
            ("system-smtp-encryption", "POSTA_SYSTEM_SMTP_ENCRYPTION"),
            ("inbound-enabled", "POSTA_INBOUND_ENABLED"),
            ("inbound-smtp-host", "POSTA_INBOUND_SMTP_HOST"),
            ("inbound-smtp-port", "POSTA_INBOUND_SMTP_PORT"),
            ("inbound-hostname", "POSTA_INBOUND_HOSTNAME"),
            ("inbound-max-message-size", "POSTA_INBOUND_MAX_MESSAGE_SIZE"),
            ("inbound-max-attach-size", "POSTA_INBOUND_MAX_ATTACH_SIZE"),
            ("inbound-webhook-secret", "POSTA_INBOUND_WEBHOOK_SECRET"),
            ("inbound-tls-mode", "POSTA_INBOUND_TLS_MODE"),
            ("inbound-tls-cert-file", "POSTA_INBOUND_TLS_CERT_FILE"),
            ("inbound-tls-key-file", "POSTA_INBOUND_TLS_KEY_FILE"),
            ("inbound-smtp-rate-limit", "POSTA_INBOUND_SMTP_RATE_LIMIT"),
            ("inbound-smtp-rate-window", "POSTA_INBOUND_SMTP_RATE_WINDOW"),
            ("email-verification-required", "POSTA_EMAIL_VERIFICATION_REQUIRED"),
            ("auto-suppress-on-reject", "POSTA_AUTO_SUPPRESS_ON_REJECT"),
            ("email-verify-enabled", "POSTA_EMAIL_VERIFY_ENABLED"),
            ("email-verify-cache-ttl-hours", "POSTA_EMAIL_VERIFY_CACHE_TTL_HOURS"),
            ("email-verify-mx-cache-ttl-hours", "POSTA_EMAIL_VERIFY_MX_CACHE_TTL_HOURS"),
            ("email-verify-rate-hourly", "POSTA_EMAIL_VERIFY_RATE_HOURLY"),
            ("allow-downgrade", "POSTA_ALLOW_DOWNGRADE"),
            ("plan-enforcement", "POSTA_PLAN_ENFORCEMENT"),
            ("db-url", "POSTA_DB_URL")
        })
        {
            AssertParameter(parameterName, environmentVariableName);
        }

        Assert.True(env.ContainsKey("POSTA_DB_HOST"));
        Assert.True(env.ContainsKey("POSTA_DB_PORT"));
        Assert.True(env.ContainsKey("POSTA_DB_USER"));
        Assert.True(env.ContainsKey("POSTA_DB_PASSWORD"));
        Assert.Equal("posta", env["POSTA_DB_NAME"]);
        Assert.Equal("disable", env["POSTA_DB_SSL_MODE"]);
        Assert.True(env.ContainsKey("POSTA_REDIS_ADDR"));
        AssertParameter("redis-address", "POSTA_REDIS_ADDR");
        AssertParameter("redis-password", "POSTA_REDIS_PASSWORD");

        void AssertParameter(string parameterName, string environmentVariableName)
        {
            Assert.Same(parameters[parameterName].Resource, env[environmentVariableName]);
        }
    }

    private static (IResourceBuilder<PostgresDatabaseResource> Database, IResourceBuilder<RedisResource> Redis) AddPostaDependencies(IDistributedApplicationBuilder builder)
    {
        var postgres = builder.AddPostgres("postgres");
        var database = postgres.AddDatabase("posta-db", "posta");
        var redis = builder.AddRedis("redis");

        return (database, redis);
    }
}
