using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Posta;

/// <summary>
/// Parameter-based configuration overrides for the Posta container.
/// </summary>
public sealed class PostaOptions
{
    /// <summary>
    /// Gets or sets the Posta environment name.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Environment { get; set; }

    /// <summary>
    /// Gets or sets whether Posta stores emails without sending them.
    /// </summary>
    public IResourceBuilder<ParameterResource>? DevMode { get; set; }

    /// <summary>
    /// Gets or sets whether auth rate limiting is enabled.
    /// </summary>
    public IResourceBuilder<ParameterResource>? AuthRateLimitEnabled { get; set; }

    /// <summary>
    /// Gets or sets the maximum emails per hour per user.
    /// </summary>
    public IResourceBuilder<ParameterResource>? RateLimitHourly { get; set; }

    /// <summary>
    /// Gets or sets the maximum emails per day per user.
    /// </summary>
    public IResourceBuilder<ParameterResource>? RateLimitDaily { get; set; }

    /// <summary>
    /// Gets or sets whether Swagger UI and ReDoc are enabled.
    /// </summary>
    public IResourceBuilder<ParameterResource>? OpenApiDocs { get; set; }

    /// <summary>
    /// Gets or sets whether Prometheus metrics are enabled.
    /// </summary>
    public IResourceBuilder<ParameterResource>? MetricsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the Vue dashboard build directory.
    /// </summary>
    public IResourceBuilder<ParameterResource>? WebDir { get; set; }

    /// <summary>
    /// Gets or sets the public dashboard URL.
    /// </summary>
    public IResourceBuilder<ParameterResource>? WebUrl { get; set; }

    /// <summary>
    /// Gets or sets the public API base URL advertised in the OpenAPI servers list.
    /// </summary>
    public IResourceBuilder<ParameterResource>? ApiUrl { get; set; }

    /// <summary>
    /// Gets or sets the comma-separated allowed CORS origins.
    /// </summary>
    public IResourceBuilder<ParameterResource>? CorsOrigins { get; set; }

    /// <summary>
    /// Gets or sets whether the worker runs in the server process.
    /// </summary>
    public IResourceBuilder<ParameterResource>? EmbeddedWorker { get; set; }

    /// <summary>
    /// Gets or sets the number of worker goroutines.
    /// </summary>
    public IResourceBuilder<ParameterResource>? WorkerConcurrency { get; set; }

    /// <summary>
    /// Gets or sets the maximum email retry attempts.
    /// </summary>
    public IResourceBuilder<ParameterResource>? WorkerMaxRetries { get; set; }

    /// <summary>
    /// Gets or sets the maximum webhook delivery retry attempts.
    /// </summary>
    public IResourceBuilder<ParameterResource>? WebhookMaxRetries { get; set; }

    /// <summary>
    /// Gets or sets the webhook HTTP request timeout in seconds.
    /// </summary>
    public IResourceBuilder<ParameterResource>? WebhookTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the optional HTTP, HTTPS, or SOCKS5 proxy for webhook delivery.
    /// </summary>
    public IResourceBuilder<ParameterResource>? WebhookProxyUrl { get; set; }

    /// <summary>
    /// Gets or sets the Google OAuth client ID for SSO login.
    /// </summary>
    public IResourceBuilder<ParameterResource>? GoogleOAuthClientId { get; set; }

    /// <summary>
    /// Gets or sets the Google OAuth client secret for SSO login.
    /// </summary>
    public IResourceBuilder<ParameterResource>? GoogleOAuthClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the OAuth callback base URL.
    /// </summary>
    public IResourceBuilder<ParameterResource>? OAuthCallbackUrl { get; set; }

    /// <summary>
    /// Gets or sets the storage backend for attachments, either s3 or filesystem.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobProvider { get; set; }

    /// <summary>
    /// Gets or sets the S3-compatible endpoint.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobS3Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the S3 region.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobS3Region { get; set; }

    /// <summary>
    /// Gets or sets the S3 bucket name.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobS3Bucket { get; set; }

    /// <summary>
    /// Gets or sets the S3 access key.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobS3AccessKey { get; set; }

    /// <summary>
    /// Gets or sets the S3 secret key.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobS3SecretKey { get; set; }

    /// <summary>
    /// Gets or sets whether S3 storage uses TLS.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobS3UseSsl { get; set; }

    /// <summary>
    /// Gets or sets whether S3 path-style addressing is used.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobS3PathStyle { get; set; }

    /// <summary>
    /// Gets or sets the filesystem storage path.
    /// </summary>
    public IResourceBuilder<ParameterResource>? BlobFileSystemPath { get; set; }

    /// <summary>
    /// Gets or sets the AES-256-GCM encryption key for stored SMTP passwords.
    /// </summary>
    public IResourceBuilder<ParameterResource>? EncryptionKey { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP server host used for platform notifications.
    /// </summary>
    public IResourceBuilder<ParameterResource>? SystemSmtpHost { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP server port.
    /// </summary>
    public IResourceBuilder<ParameterResource>? SystemSmtpPort { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP username.
    /// </summary>
    public IResourceBuilder<ParameterResource>? SystemSmtpUsername { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP password.
    /// </summary>
    public IResourceBuilder<ParameterResource>? SystemSmtpPassword { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP from address.
    /// </summary>
    public IResourceBuilder<ParameterResource>? SystemSmtpFrom { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP encryption mode: none, ssl, or starttls.
    /// </summary>
    public IResourceBuilder<ParameterResource>? SystemSmtpEncryption { get; set; }

    /// <summary>
    /// Gets or sets whether inbound email processing is enabled.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundEnabled { get; set; }

    /// <summary>
    /// Gets or sets the bind address for the built-in SMTP receiver.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundSmtpHost { get; set; }

    /// <summary>
    /// Gets or sets the SMTP listener port.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundSmtpPort { get; set; }

    /// <summary>
    /// Gets or sets the hostname announced in EHLO and used as TLS SNI.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundHostname { get; set; }

    /// <summary>
    /// Gets or sets the maximum raw inbound message size in bytes.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundMaxMessageSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum inbound attachment size in bytes.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundMaxAttachSize { get; set; }

    /// <summary>
    /// Gets or sets the shared secret for inbound webhooks.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundWebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets the inbound SMTP TLS mode: none or starttls.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundTlsMode { get; set; }

    /// <summary>
    /// Gets or sets the PEM certificate path for inbound TLS.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundTlsCertFile { get; set; }

    /// <summary>
    /// Gets or sets the PEM key path for inbound TLS.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundTlsKeyFile { get; set; }

    /// <summary>
    /// Gets or sets the per-IP SMTP session rate limit.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundSmtpRateLimit { get; set; }

    /// <summary>
    /// Gets or sets the SMTP rate-limit window in seconds.
    /// </summary>
    public IResourceBuilder<ParameterResource>? InboundSmtpRateWindow { get; set; }

    /// <summary>
    /// Gets or sets whether users must verify their email address before sign-in.
    /// </summary>
    public IResourceBuilder<ParameterResource>? EmailVerificationRequired { get; set; }

    /// <summary>
    /// Gets or sets whether permanently rejected recipients are added to the suppression list.
    /// </summary>
    public IResourceBuilder<ParameterResource>? AutoSuppressOnReject { get; set; }

    /// <summary>
    /// Gets or sets whether the email verification endpoint is enabled.
    /// </summary>
    public IResourceBuilder<ParameterResource>? EmailVerifyEnabled { get; set; }

    /// <summary>
    /// Gets or sets how long address-level verification results are cached, in hours.
    /// </summary>
    public IResourceBuilder<ParameterResource>? EmailVerifyCacheTtlHours { get; set; }

    /// <summary>
    /// Gets or sets how long domain MX lookups are cached, in hours.
    /// </summary>
    public IResourceBuilder<ParameterResource>? EmailVerifyMxCacheTtlHours { get; set; }

    /// <summary>
    /// Gets or sets the per-user hourly cap on verification requests.
    /// </summary>
    public IResourceBuilder<ParameterResource>? EmailVerifyRateHourly { get; set; }

    /// <summary>
    /// Gets or sets whether older binaries can boot against a newer database schema.
    /// </summary>
    public IResourceBuilder<ParameterResource>? AllowDowngrade { get; set; }

    /// <summary>
    /// Gets or sets whether hosted plan limits and quotas are enforced.
    /// </summary>
    public IResourceBuilder<ParameterResource>? PlanEnforcement { get; set; }

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
    public IResourceBuilder<ParameterResource>? RedisAddress { get; set; }
}
