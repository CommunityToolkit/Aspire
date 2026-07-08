using Aspire.Hosting;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Posta.Tests;

public class ContainerResourceCreationTests
{
    [Fact]
    public void AddPostaThrowsWhenBuilderIsNull()
    {
        IDistributedApplicationBuilder builder = null!;

        var exception = Assert.Throws<ArgumentNullException>(() => builder.AddPosta("posta"));
        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddPostaThrowsWhenNameIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddPosta(null!));
    }

    [Fact]
    public void WithReferenceThrowsWhenDatabaseIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var posta = builder.AddPosta("posta");

        Assert.Throws<ArgumentNullException>(() => posta.WithReference((IResourceBuilder<PostgresDatabaseResource>)null!));
    }

    [Fact]
    public void WithReferenceThrowsWhenRedisIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var posta = builder.AddPosta("posta");

        Assert.Throws<ArgumentNullException>(() => posta.WithReference((IResourceBuilder<RedisResource>)null!));
    }

    [Fact]
    public void AddPostaSetsContainerDetailsOnResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddPosta("posta");

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

        builder.AddPosta("posta", port: 9001);

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

        builder.AddPosta("posta");

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(appModel.Resources.OfType<PostaResource>());

        Assert.True(resource.TryGetAnnotationsOfType<HealthCheckAnnotation>(out var annotations));
        Assert.Equal(2, annotations.Count());
    }

    [Fact]
    public async Task AddPostaConfiguresPostaEnvironmentVariables()
    {
        var builder = DistributedApplication.CreateBuilder();
        var postgres = builder.AddPostgres("postgres");
        var database = postgres.AddDatabase("posta-db", "posta");
        var redis = builder.AddRedis("redis");
        var databaseUrl = builder.AddParameter("posta-db-url", "postgres://example");
        var redisPassword = builder.AddParameter("posta-redis-password", "redis-secret");
        var googleSecret = builder.AddParameter("posta-google-secret", "google-secret");
        var s3AccessKey = builder.AddParameter("posta-s3-access-key", "s3-access");
        var s3SecretKey = builder.AddParameter("posta-s3-secret-key", "s3-secret");
        var encryptionKey = builder.AddParameter("posta-encryption-key", "encryption-secret");
        var smtpPassword = builder.AddParameter("posta-smtp-password", "smtp-secret");
        var inboundWebhookSecret = builder.AddParameter("posta-inbound-secret", "inbound-secret");

        var posta = builder.AddPosta("posta", options =>
        {
            options.Environment = "dev";
            options.DevMode = true;
            options.AuthRateLimitEnabled = false;
            options.RateLimitHourly = 500;
            options.RateLimitDaily = 5000;
            options.OpenApiDocs = false;
            options.MetricsEnabled = true;
            options.WebDir = "web/dist";
            options.WebUrl = "https://posta.example.com";
            options.ApiUrl = "https://api.posta.example.com";
            options.CorsOrigins = "https://app.example.com";
            options.EmbeddedWorker = false;
            options.WorkerConcurrency = 20;
            options.WorkerMaxRetries = 7;
            options.WebhookMaxRetries = 4;
            options.WebhookTimeoutSeconds = 30;
            options.WebhookProxyUrl = "http://proxy.example.com";
            options.GoogleOAuthClientId = "google-client-id";
            options.GoogleOAuthClientSecret = googleSecret;
            options.OAuthCallbackUrl = "https://posta.example.com/oauth/callback";
            options.BlobProvider = "s3";
            options.BlobS3Endpoint = "https://s3.example.com";
            options.BlobS3Region = "eu-central-1";
            options.BlobS3Bucket = "posta";
            options.BlobS3AccessKey = s3AccessKey;
            options.BlobS3SecretKey = s3SecretKey;
            options.BlobS3UseSsl = false;
            options.BlobS3PathStyle = true;
            options.BlobFileSystemPath = "/posta/attachments";
            options.EncryptionKey = encryptionKey;
            options.SystemSmtpHost = "smtp.example.com";
            options.SystemSmtpPort = 2525;
            options.SystemSmtpUsername = "notifications@example.com";
            options.SystemSmtpPassword = smtpPassword;
            options.SystemSmtpFrom = "notifications@example.com";
            options.SystemSmtpEncryption = "ssl";
            options.InboundEnabled = true;
            options.InboundSmtpHost = "127.0.0.1";
            options.InboundSmtpPort = 2526;
            options.InboundHostname = "mx.example.com";
            options.InboundMaxMessageSize = 12345;
            options.InboundMaxAttachSize = 6789;
            options.InboundWebhookSecret = inboundWebhookSecret;
            options.InboundTlsMode = "starttls";
            options.InboundTlsCertFile = "/certs/fullchain.pem";
            options.InboundTlsKeyFile = "/certs/privkey.pem";
            options.InboundSmtpRateLimit = 10;
            options.InboundSmtpRateWindow = 20;
            options.EmailVerificationRequired = true;
            options.AutoSuppressOnReject = false;
            options.EmailVerifyEnabled = false;
            options.EmailVerifyCacheTtlHours = 12;
            options.EmailVerifyMxCacheTtlHours = 6;
            options.EmailVerifyRateHourly = 42;
            options.AllowDowngrade = true;
            options.PlanEnforcement = true;
            options.DatabaseUrl = databaseUrl;
            options.RedisPassword = redisPassword;
            options.RedisAddress = "redis.example.com:6379";
        })
            .WithReference(database)
            .WithReference(redis, redisPassword);

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
        Assert.Equal("dev", env["POSTA_ENV"]);
        Assert.Equal("true", env["POSTA_DEV_MODE"]);
        Assert.Equal("false", env["POSTA_AUTH_RATE_LIMIT_ENABLED"]);
        Assert.Equal("500", env["POSTA_RATE_LIMIT_HOURLY"]);
        Assert.Equal("5000", env["POSTA_RATE_LIMIT_DAILY"]);
        Assert.Equal("false", env["POSTA_OPENAPI_DOCS"]);
        Assert.Equal("true", env["POSTA_METRICS_ENABLED"]);
        Assert.Equal("web/dist", env["POSTA_WEB_DIR"]);
        Assert.Equal("https://posta.example.com", env["POSTA_WEB_URL"]);
        Assert.Equal("https://api.posta.example.com", env["POSTA_API_URL"]);
        Assert.Equal("https://app.example.com", env["POSTA_CORS_ORIGINS"]);
        Assert.Equal("false", env["POSTA_EMBEDDED_WORKER"]);
        Assert.Equal("20", env["POSTA_WORKER_CONCURRENCY"]);
        Assert.Equal("7", env["POSTA_WORKER_MAX_RETRIES"]);
        Assert.Equal("4", env["POSTA_WEBHOOK_MAX_RETRIES"]);
        Assert.Equal("30", env["POSTA_WEBHOOK_TIMEOUT_SECS"]);
        Assert.Equal("http://proxy.example.com", env["POSTA_WEBHOOK_PROXY_URL"]);
        Assert.Equal("google-client-id", env["POSTA_GOOGLE_OAUTH_CLIENT_ID"]);
        Assert.Same(googleSecret.Resource, env["POSTA_GOOGLE_OAUTH_CLIENT_SECRET"]);
        Assert.Equal("https://posta.example.com/oauth/callback", env["POSTA_OAUTH_CALLBACK_URL"]);
        Assert.Equal("s3", env["POSTA_BLOB_PROVIDER"]);
        Assert.Equal("https://s3.example.com", env["POSTA_BLOB_S3_ENDPOINT"]);
        Assert.Equal("eu-central-1", env["POSTA_BLOB_S3_REGION"]);
        Assert.Equal("posta", env["POSTA_BLOB_S3_BUCKET"]);
        Assert.Same(s3AccessKey.Resource, env["POSTA_BLOB_S3_ACCESS_KEY"]);
        Assert.Same(s3SecretKey.Resource, env["POSTA_BLOB_S3_SECRET_KEY"]);
        Assert.Equal("false", env["POSTA_BLOB_S3_USE_SSL"]);
        Assert.Equal("true", env["POSTA_BLOB_S3_PATH_STYLE"]);
        Assert.Equal("/posta/attachments", env["POSTA_BLOB_FS_PATH"]);
        Assert.Same(encryptionKey.Resource, env["POSTA_ENCRYPTION_KEY"]);
        Assert.Equal("smtp.example.com", env["POSTA_SYSTEM_SMTP_HOST"]);
        Assert.Equal("2525", env["POSTA_SYSTEM_SMTP_PORT"]);
        Assert.Equal("notifications@example.com", env["POSTA_SYSTEM_SMTP_USERNAME"]);
        Assert.Same(smtpPassword.Resource, env["POSTA_SYSTEM_SMTP_PASSWORD"]);
        Assert.Equal("notifications@example.com", env["POSTA_SYSTEM_SMTP_FROM"]);
        Assert.Equal("ssl", env["POSTA_SYSTEM_SMTP_ENCRYPTION"]);
        Assert.Equal("true", env["POSTA_INBOUND_ENABLED"]);
        Assert.Equal("127.0.0.1", env["POSTA_INBOUND_SMTP_HOST"]);
        Assert.Equal("2526", env["POSTA_INBOUND_SMTP_PORT"]);
        Assert.Equal("mx.example.com", env["POSTA_INBOUND_HOSTNAME"]);
        Assert.Equal("12345", env["POSTA_INBOUND_MAX_MESSAGE_SIZE"]);
        Assert.Equal("6789", env["POSTA_INBOUND_MAX_ATTACH_SIZE"]);
        Assert.Same(inboundWebhookSecret.Resource, env["POSTA_INBOUND_WEBHOOK_SECRET"]);
        Assert.Equal("starttls", env["POSTA_INBOUND_TLS_MODE"]);
        Assert.Equal("/certs/fullchain.pem", env["POSTA_INBOUND_TLS_CERT_FILE"]);
        Assert.Equal("/certs/privkey.pem", env["POSTA_INBOUND_TLS_KEY_FILE"]);
        Assert.Equal("10", env["POSTA_INBOUND_SMTP_RATE_LIMIT"]);
        Assert.Equal("20", env["POSTA_INBOUND_SMTP_RATE_WINDOW"]);
        Assert.Equal("true", env["POSTA_EMAIL_VERIFICATION_REQUIRED"]);
        Assert.Equal("false", env["POSTA_AUTO_SUPPRESS_ON_REJECT"]);
        Assert.Equal("false", env["POSTA_EMAIL_VERIFY_ENABLED"]);
        Assert.Equal("12", env["POSTA_EMAIL_VERIFY_CACHE_TTL_HOURS"]);
        Assert.Equal("6", env["POSTA_EMAIL_VERIFY_MX_CACHE_TTL_HOURS"]);
        Assert.Equal("42", env["POSTA_EMAIL_VERIFY_RATE_HOURLY"]);
        Assert.Equal("true", env["POSTA_ALLOW_DOWNGRADE"]);
        Assert.Equal("true", env["POSTA_PLAN_ENFORCEMENT"]);
        Assert.Same(databaseUrl.Resource, env["POSTA_DB_URL"]);
        Assert.True(env.ContainsKey("POSTA_DB_HOST"));
        Assert.True(env.ContainsKey("POSTA_DB_PORT"));
        Assert.True(env.ContainsKey("POSTA_DB_USER"));
        Assert.True(env.ContainsKey("POSTA_DB_PASSWORD"));
        Assert.Equal("posta", env["POSTA_DB_NAME"]);
        Assert.Equal("disable", env["POSTA_DB_SSL_MODE"]);
        Assert.True(env.ContainsKey("POSTA_REDIS_ADDR"));
        Assert.NotEqual("redis.example.com:6379", env["POSTA_REDIS_ADDR"].ToString());
        Assert.Same(redisPassword.Resource, env["POSTA_REDIS_PASSWORD"]);
    }
}