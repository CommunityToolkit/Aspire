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
    public void WithDataVolumeAddsVolumeMount()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);

        var posta = builder.AddPosta("posta", database, redis)
            .WithDataVolume("posta-data", isReadOnly: true);

        var mount = Assert.Single(posta.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.Equal("posta-data", mount.Source);
        Assert.Equal("/data", mount.Target);
        Assert.True(mount.IsReadOnly);
    }

    [Fact]
    public void WithDataVolumeGeneratesVolumeNameWhenNotSpecified()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);

        var posta = builder.AddPosta("posta", database, redis)
            .WithDataVolume();

        var mount = Assert.Single(posta.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(ContainerMountType.Volume, mount.Type);
        Assert.False(string.IsNullOrWhiteSpace(mount.Source));
        Assert.Equal("/data", mount.Target);
    }

    [Fact]
    public void WithDataBindMountAddsBindMount()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);

        var posta = builder.AddPosta("posta", database, redis)
            .WithDataBindMount("./posta-data", isReadOnly: true);

        var mount = Assert.Single(posta.Resource.Annotations.OfType<ContainerMountAnnotation>());
        Assert.Equal(ContainerMountType.BindMount, mount.Type);
        var source = Assert.IsType<string>(mount.Source);
        Assert.True(Path.IsPathFullyQualified(source));
        Assert.EndsWith("posta-data", source);
        Assert.Equal("/data", mount.Target);
        Assert.True(mount.IsReadOnly);
    }

    [Fact]
    public void WithDataMethodsValidateArguments()
    {
        IResourceBuilder<PostaResource> posta = null!;

        Assert.Throws<ArgumentNullException>(() => posta.WithDataVolume());
        Assert.Throws<ArgumentNullException>(() => posta.WithDataBindMount("./posta-data"));

        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);
        posta = builder.AddPosta("posta", database, redis);

        Assert.Throws<ArgumentNullException>(() => posta.WithDataBindMount(null!));
    }

    [Fact]
    public void WithGroupedOptionCallbacksThrowWhenConfigureOptionsIsNull()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);
        var posta = builder.AddPosta("posta", database, redis);

        Assert.Throws<ArgumentNullException>(() => posta.WithSystemSmtp((Action<PostaSystemSmtpOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => posta.WithInboundSmtp((Action<PostaInboundSmtpOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => posta.WithS3BlobStorage((Action<PostaS3BlobStorageOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => posta.WithGoogleOAuth((Action<PostaGoogleOAuthOptions>)null!));
        Assert.Throws<ArgumentNullException>(() => posta.WithEmailVerification((Action<PostaEmailVerificationOptions>)null!));
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
        var databaseUrl = builder.AddParameter("posta-db-url", "postgres://example");
        var redisPassword = builder.AddParameter("posta-redis-password", "redis-secret");
        var googleSecret = builder.AddParameter("posta-google-secret", "google-secret");
        var s3AccessKey = builder.AddParameter("posta-s3-access-key", "s3-access");
        var s3SecretKey = builder.AddParameter("posta-s3-secret-key", "s3-secret");
        var encryptionKey = builder.AddParameter("posta-encryption-key", "encryption-secret");
        var smtpPassword = builder.AddParameter("posta-smtp-password", "smtp-secret");
        var inboundWebhookSecret = builder.AddParameter("posta-inbound-secret", "inbound-secret");

        var posta = builder.AddPosta("posta", database, redis, options =>
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
        Assert.Equal("redis.example.com:6379", env["POSTA_REDIS_ADDR"]);
        Assert.Same(redisPassword.Resource, env["POSTA_REDIS_PASSWORD"]);
    }

    [Fact]
    public async Task WithGroupedOptionsConfiguresParameterBasedEnvironmentVariables()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);
        var parameters = new Dictionary<string, IResourceBuilder<ParameterResource>>();

        IResourceBuilder<ParameterResource> Parameter(string name, string value)
        {
            var parameter = builder.AddParameter($"posta-{name}", value);
            parameters[name] = parameter;
            return parameter;
        }

        var posta = builder.AddPosta("posta", database, redis)
            .WithSystemSmtp(new PostaSystemSmtpOptions
            {
                Host = Parameter("system-smtp-host", "smtp.example.com"),
                Port = Parameter("system-smtp-port", "2525"),
                Username = Parameter("system-smtp-username", "notifications@example.com"),
                Password = Parameter("system-smtp-password", "smtp-secret"),
                From = Parameter("system-smtp-from", "notifications@example.com"),
                Encryption = Parameter("system-smtp-encryption", "ssl")
            })
            .WithInboundSmtp(new PostaInboundSmtpOptions
            {
                Enabled = Parameter("inbound-enabled", "true"),
                Host = Parameter("inbound-smtp-host", "127.0.0.1"),
                Port = Parameter("inbound-smtp-port", "2526"),
                Hostname = Parameter("inbound-hostname", "mx.example.com"),
                MaxMessageSize = Parameter("inbound-max-message-size", "12345"),
                MaxAttachmentSize = Parameter("inbound-max-attachment-size", "6789"),
                WebhookSecret = Parameter("inbound-webhook-secret", "inbound-secret"),
                TlsMode = Parameter("inbound-tls-mode", "starttls"),
                TlsCertFile = Parameter("inbound-tls-cert-file", "/certs/fullchain.pem"),
                TlsKeyFile = Parameter("inbound-tls-key-file", "/certs/privkey.pem"),
                RateLimit = Parameter("inbound-smtp-rate-limit", "10"),
                RateWindow = Parameter("inbound-smtp-rate-window", "20")
            })
            .WithS3BlobStorage(new PostaS3BlobStorageOptions
            {
                Endpoint = Parameter("blob-s3-endpoint", "https://s3.example.com"),
                Region = Parameter("blob-s3-region", "eu-central-1"),
                Bucket = Parameter("blob-s3-bucket", "posta"),
                AccessKey = Parameter("blob-s3-access-key", "s3-access"),
                SecretKey = Parameter("blob-s3-secret-key", "s3-secret"),
                UseSsl = Parameter("blob-s3-use-ssl", "false"),
                PathStyle = Parameter("blob-s3-path-style", "true")
            })
            .WithGoogleOAuth(new PostaGoogleOAuthOptions
            {
                ClientId = Parameter("google-oauth-client-id", "google-client-id"),
                ClientSecret = Parameter("google-oauth-client-secret", "google-secret"),
                CallbackUrl = Parameter("oauth-callback-url", "https://posta.example.com/oauth/callback")
            })
            .WithEmailVerification(new PostaEmailVerificationOptions
            {
                Required = Parameter("email-verification-required", "true"),
                AutoSuppressOnReject = Parameter("auto-suppress-on-reject", "false"),
                Enabled = Parameter("email-verify-enabled", "false"),
                CacheTtlHours = Parameter("email-verify-cache-ttl-hours", "12"),
                MxCacheTtlHours = Parameter("email-verify-mx-cache-ttl-hours", "6"),
                RateHourly = Parameter("email-verify-rate-hourly", "42")
            });

        Assert.True(posta.Resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var annotations));

        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(
                new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));

        foreach (var annotation in annotations)
        {
            await annotation.Callback(context);
        }

        var env = context.EnvironmentVariables;

        Assert.Equal("s3", env["POSTA_BLOB_PROVIDER"]);
        foreach (var (parameterName, environmentVariableName) in new (string ParameterName, string EnvironmentVariableName)[]
        {
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
            ("inbound-max-attachment-size", "POSTA_INBOUND_MAX_ATTACH_SIZE"),
            ("inbound-webhook-secret", "POSTA_INBOUND_WEBHOOK_SECRET"),
            ("inbound-tls-mode", "POSTA_INBOUND_TLS_MODE"),
            ("inbound-tls-cert-file", "POSTA_INBOUND_TLS_CERT_FILE"),
            ("inbound-tls-key-file", "POSTA_INBOUND_TLS_KEY_FILE"),
            ("inbound-smtp-rate-limit", "POSTA_INBOUND_SMTP_RATE_LIMIT"),
            ("inbound-smtp-rate-window", "POSTA_INBOUND_SMTP_RATE_WINDOW"),
            ("blob-s3-endpoint", "POSTA_BLOB_S3_ENDPOINT"),
            ("blob-s3-region", "POSTA_BLOB_S3_REGION"),
            ("blob-s3-bucket", "POSTA_BLOB_S3_BUCKET"),
            ("blob-s3-access-key", "POSTA_BLOB_S3_ACCESS_KEY"),
            ("blob-s3-secret-key", "POSTA_BLOB_S3_SECRET_KEY"),
            ("blob-s3-use-ssl", "POSTA_BLOB_S3_USE_SSL"),
            ("blob-s3-path-style", "POSTA_BLOB_S3_PATH_STYLE"),
            ("google-oauth-client-id", "POSTA_GOOGLE_OAUTH_CLIENT_ID"),
            ("google-oauth-client-secret", "POSTA_GOOGLE_OAUTH_CLIENT_SECRET"),
            ("oauth-callback-url", "POSTA_OAUTH_CALLBACK_URL"),
            ("email-verification-required", "POSTA_EMAIL_VERIFICATION_REQUIRED"),
            ("auto-suppress-on-reject", "POSTA_AUTO_SUPPRESS_ON_REJECT"),
            ("email-verify-enabled", "POSTA_EMAIL_VERIFY_ENABLED"),
            ("email-verify-cache-ttl-hours", "POSTA_EMAIL_VERIFY_CACHE_TTL_HOURS"),
            ("email-verify-mx-cache-ttl-hours", "POSTA_EMAIL_VERIFY_MX_CACHE_TTL_HOURS"),
            ("email-verify-rate-hourly", "POSTA_EMAIL_VERIFY_RATE_HOURLY")
        })
        {
            Assert.Same(parameters[parameterName].Resource, env[environmentVariableName]);
        }
    }

    [Fact]
    public async Task WithGroupedOptionCallbacksConfigureParameterBasedEnvironmentVariables()
    {
        var builder = DistributedApplication.CreateBuilder();
        var (database, redis) = AddPostaDependencies(builder);
        var smtpHost = builder.AddParameter("posta-smtp-host", "smtp.example.com");
        var inboundEnabled = builder.AddParameter("posta-inbound-enabled", "true");
        var s3Bucket = builder.AddParameter("posta-s3-bucket", "posta");
        var googleClientId = builder.AddParameter("posta-google-client-id", "google-client-id");
        var emailVerificationRequired = builder.AddParameter("posta-email-verification-required", "true");

        var posta = builder.AddPosta("posta", database, redis)
            .WithSystemSmtp(options => options.Host = smtpHost)
            .WithInboundSmtp(options => options.Enabled = inboundEnabled)
            .WithS3BlobStorage(options => options.Bucket = s3Bucket)
            .WithGoogleOAuth(options => options.ClientId = googleClientId)
            .WithEmailVerification(options => options.Required = emailVerificationRequired);

        Assert.True(posta.Resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var annotations));

        var context = new EnvironmentCallbackContext(
            new DistributedApplicationExecutionContext(
                new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Run)));

        foreach (var annotation in annotations)
        {
            await annotation.Callback(context);
        }

        var env = context.EnvironmentVariables;

        Assert.Same(smtpHost.Resource, env["POSTA_SYSTEM_SMTP_HOST"]);
        Assert.Same(inboundEnabled.Resource, env["POSTA_INBOUND_ENABLED"]);
        Assert.Equal("s3", env["POSTA_BLOB_PROVIDER"]);
        Assert.Same(s3Bucket.Resource, env["POSTA_BLOB_S3_BUCKET"]);
        Assert.Same(googleClientId.Resource, env["POSTA_GOOGLE_OAUTH_CLIENT_ID"]);
        Assert.Same(emailVerificationRequired.Resource, env["POSTA_EMAIL_VERIFICATION_REQUIRED"]);
    }

    private static (IResourceBuilder<PostgresDatabaseResource> Database, IResourceBuilder<RedisResource> Redis) AddPostaDependencies(IDistributedApplicationBuilder builder)
    {
        var postgres = builder.AddPostgres("postgres");
        var database = postgres.AddDatabase("posta-db", "posta");
        var redis = builder.AddRedis("redis");

        return (database, redis);
    }
}
