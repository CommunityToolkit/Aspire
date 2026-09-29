using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Posta;

/// <summary>
/// Parameter-based configuration options for Posta system SMTP notifications.
/// </summary>
public sealed class PostaSystemSmtpOptions
{
    /// <summary>
    /// Gets or sets the system SMTP server host used for platform notifications.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Host { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP server port.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Port { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP username.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Username { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP password.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Password { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP from address.
    /// </summary>
    public IResourceBuilder<ParameterResource>? From { get; set; }

    /// <summary>
    /// Gets or sets the system SMTP encryption mode: none, ssl, or starttls.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Encryption { get; set; }
}
