using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Posta;

/// <summary>
/// Configuration options for the Posta container.
/// </summary>
public sealed class PostaOptions
{
    /// <summary>
    /// Gets or sets the Posta environment name.
    /// </summary>
    public string Environment { get; set; } = "production";

    /// <summary>
    /// Gets or sets a value indicating whether Posta stores emails without sending them.
    /// </summary>
    public bool DevMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether auth rate limiting is enabled.
    /// </summary>
    public bool AuthRateLimitEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum emails per hour per user.
    /// </summary>
    public int RateLimitHourly { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum emails per day per user.
    /// </summary>
    public int RateLimitDaily { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether Swagger UI and ReDoc are enabled.
    /// </summary>
    public bool OpenApiDocs { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Prometheus metrics are enabled.
    /// </summary>
    public bool MetricsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the Vue dashboard build directory.
    /// </summary>
    public string? WebDir { get; set; }

    /// <summary>
    /// Gets or sets the public dashboard URL.
    /// </summary>
    public string? WebUrl { get; set; }

    /// <summary>
    /// Gets or sets the public API base URL advertised in the OpenAPI servers list.
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// Gets or sets the comma-separated allowed CORS origins.
    /// </summary>
    public string CorsOrigins { get; set; } = "*";

    /// <summary>
    /// Gets or sets a value indicating whether the worker runs in the server process.
    /// </summary>
    public bool EmbeddedWorker { get; set; } = true;

    /// <summary>
    /// Gets or sets the number of worker goroutines.
    /// </summary>
    public int WorkerConcurrency { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum email retry attempts.
    /// </summary>
    public int WorkerMaxRetries { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum webhook delivery retry attempts.
    /// </summary>
    public int WebhookMaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the webhook HTTP request timeout in seconds.
    /// </summary>
    public int WebhookTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the optional HTTP, HTTPS, or SOCKS5 proxy for webhook delivery.
    /// </summary>
    public string? WebhookProxyUrl { get; set; }

    /// <summary>
    /// Gets or sets the Google OAuth client ID for SSO login.
    /// </summary>
    public string? GoogleOAuthClientId { get; set; }

    /// <summary>
    /// Gets or sets the Google OAuth client secret for SSO login.
    /// </summary>
    public IResourceBuilder<ParameterResource>? GoogleOAuthClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the OAuth callback base URL.
    /// </summary>
    public string? OAuthCallbackUrl { get; set; }

    /// <summary>
    /// Gets or sets the storage backend for attachments, either s3 or filesystem.
    /// </summary>
    public string? BlobProvider { get; set; }

    /// <summary>
    /// Gets or sets the S3-compatible endpoint.
    /// </summary>
    public string? BlobS3Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the S3 region.
    /// </summary>
    public string BlobS3Region { get; set; } = "us-east-1";

    /// <summary>
    /// Gets or sets the S3 bucket name.
    /// </summary>
    public string? BlobS3Bucket { get; set; }

    /// <summary>
    /// Gets or sets the S3 access key.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobS3AccessKey { get; set; }

    /// <summary>
    /// Gets or sets the S3 secret key.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobS3SecretKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether S3 storage uses TLS.
    /// </summary>
    public bool BlobS3UseSsl { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether S3 path-style addressing is used.
    /// </summary>
    public bool BlobS3PathStyle { get; set; }

    /// <summary>
    /// Gets or sets the filesystem storage path.
    /// </summary>
    public string BlobFileSystemPath { get; set; } = "/data/attachments";

    /// <summary>
    /// Gets or sets the AES-256-GCM encryption key for stored SMTP passwords.
    /// </summary>
    public IResourceBuilder<ParameterResource>? EncryptionKey { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP server host used for platform notifications.
    /// </summary>
    public string? SystemSmtpHost { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP server port.
    /// </summary>
    public int SystemSmtpPort { get; set; } = 587;

    /// <summary>
    /// Gets or sets the system SMTP username.
    /// </summary>
    public string? SystemSmtpUsername { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP password.
    /// </summary>
    public IResourceBuilder<ParameterResource>? SystemSmtpPassword { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP from address.
    /// </summary>
    public string? SystemSmtpFrom { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP encryption mode: none, ssl, or starttls.
    /// </summary>
    public string SystemSmtpEncryption { get; set; } = "starttls";

    /// <summary>
    /// Gets or sets a value indicating whether inbound email processing is enabled.
    /// </summary>
    public bool InboundEnabled { get; set; }

    /// <summary>
    /// Gets or sets the bind address for the built-in SMTP receiver.
    /// </summary>
    public string InboundSmtpHost { get; set; } = "0.0.0.0";

    /// <summary>
    /// Gets or sets the SMTP listener port.
    /// </summary>
    public int InboundSmtpPort { get; set; } = 2525;

    /// <summary>
    /// Gets or sets the hostname announced in EHLO and used as TLS SNI.
    /// </summary>
    public string InboundHostname { get; set; } = "posta.local";

    /// <summary>
    /// Gets or sets the maximum raw inbound message size in bytes.
    /// </summary>
    public int InboundMaxMessageSize { get; set; } = 26214400;

    /// <summary>
    /// Gets or sets the maximum inbound attachment size in bytes.
    /// </summary>
    public int InboundMaxAttachSize { get; set; } = 10485760;

    /// <summary>
    /// Gets or sets the shared secret for inbound webhooks.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundWebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets the inbound SMTP TLS mode: none or starttls.
    /// </summary>
    public string InboundTlsMode { get; set; } = "none";

    /// <summary>
    /// Gets or sets the PEM certificate path for inbound TLS.
    /// </summary>
    public string? InboundTlsCertFile { get; set; }

    /// <summary>
    /// Gets or sets the PEM key path for inbound TLS.
    /// </summary>
    public string? InboundTlsKeyFile { get; set; }

    /// <summary>
    /// Gets or sets the per-IP SMTP session rate limit.
    /// </summary>
    public int InboundSmtpRateLimit { get; set; } = 60;

    /// <summary>
    /// Gets or sets the SMTP rate-limit window in seconds.
    /// </summary>
    public int InboundSmtpRateWindow { get; set; } = 60;

    /// <summary>
    /// Gets or sets a value indicating whether users must verify their email address before sign-in.
    /// </summary>
    public bool EmailVerificationRequired { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether permanently rejected recipients are added to the suppression list.
    /// </summary>
    public bool AutoSuppressOnReject { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the email verification endpoint is enabled.
    /// </summary>
    public bool EmailVerifyEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how long address-level verification results are cached, in hours.
    /// </summary>
    public int EmailVerifyCacheTtlHours { get; set; } = 168;

    /// <summary>
    /// Gets or sets how long domain MX lookups are cached, in hours.
    /// </summary>
    public int EmailVerifyMxCacheTtlHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets the per-user hourly cap on verification requests.
    /// </summary>
    public int EmailVerifyRateHourly { get; set; } = 1000;

    /// <summary>
    /// Gets or sets a value indicating whether older binaries can boot against a newer database schema.
    /// </summary>
    public bool AllowDowngrade { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether hosted plan limits and quotas are enforced.
    /// </summary>
    public bool PlanEnforcement { get; set; }

    /// <summary>
    /// Gets or sets a PostgreSQL connection string parameter that overrides individual database settings.
    /// </summary>
    public IResourceBuilder<ParameterResource>? DatabaseUrl { get; set; }

    /// <summary>
    /// Gets or sets a Redis password parameter that overrides the referenced Redis resource password.
    /// </summary>
    public IResourceBuilder<ParameterResource>? RedisPassword { get; set; }

    /// <summary>
    /// Gets or sets an external Redis address in the form host:port.
    /// </summary>
    public string? RedisAddress { get; set; }
}
