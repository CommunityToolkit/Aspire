using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Posta;

/// <summary>
/// Parameter-based configuration options for Posta inbound SMTP processing.
/// </summary>
public sealed class PostaInboundSmtpOptions
{
    /// <summary>
    /// Gets or sets whether inbound email processing is enabled.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Enabled { get; set; }

    /// <summary>
    /// Gets or sets the bind address for the built-in SMTP receiver.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Host { get; set; }

    /// <summary>
    /// Gets or sets the SMTP listener port.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Port { get; set; }

    /// <summary>
    /// Gets or sets the hostname announced in EHLO and used as TLS SNI.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Hostname { get; set; }

    /// <summary>
    /// Gets or sets the maximum raw inbound message size in bytes.
    /// </summary>
    public IResourceBuilder<ParameterResource>? MaxMessageSize { get; set; }

    /// <summary>
    /// Gets or sets the maximum inbound attachment size in bytes.
    /// </summary>
    public IResourceBuilder<ParameterResource>? MaxAttachmentSize { get; set; }

    /// <summary>
    /// Gets or sets the shared secret for inbound webhooks.
    /// </summary>
    public IResourceBuilder<ParameterResource>? WebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets the inbound SMTP TLS mode: none or starttls.
    /// </summary>
    public IResourceBuilder<ParameterResource>? TlsMode { get; set; }

    /// <summary>
    /// Gets or sets the PEM certificate path for inbound TLS.
    /// </summary>
    public IResourceBuilder<ParameterResource>? TlsCertFile { get; set; }

    /// <summary>
    /// Gets or sets the PEM key path for inbound TLS.
    /// </summary>
    public IResourceBuilder<ParameterResource>? TlsKeyFile { get; set; }

    /// <summary>
    /// Gets or sets the per-IP SMTP session rate limit.
    /// </summary>
    public IResourceBuilder<ParameterResource>? RateLimit { get; set; }

    /// <summary>
    /// Gets or sets the SMTP rate-limit window in seconds.
    /// </summary>
    public IResourceBuilder<ParameterResource>? RateWindow { get; set; }
}
