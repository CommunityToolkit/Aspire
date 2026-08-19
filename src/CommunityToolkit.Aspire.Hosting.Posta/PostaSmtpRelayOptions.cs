using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Posta;

/// <summary>
/// Parameter-based configuration options for Posta's authenticated SMTP relay.
/// </summary>
public sealed class PostaSmtpRelayOptions
{
    /// <summary>Gets or sets whether the SMTP relay is enabled.</summary>
    public IResourceBuilder<ParameterResource>? Enabled { get; set; }
    /// <summary>Gets or sets the relay bind address.</summary>
    public IResourceBuilder<ParameterResource>? Host { get; set; }
    /// <summary>Gets or sets the relay listener port.</summary>
    public IResourceBuilder<ParameterResource>? Port { get; set; }
    /// <summary>Gets or sets the hostname announced by the relay.</summary>
    public IResourceBuilder<ParameterResource>? Hostname { get; set; }
    /// <summary>Gets or sets the maximum message size in bytes.</summary>
    public IResourceBuilder<ParameterResource>? MaxMessageSize { get; set; }
    /// <summary>Gets or sets the per-IP session rate limit.</summary>
    public IResourceBuilder<ParameterResource>? RateLimit { get; set; }
    /// <summary>Gets or sets the rate-limit window in seconds.</summary>
    public IResourceBuilder<ParameterResource>? RateWindow { get; set; }
}
