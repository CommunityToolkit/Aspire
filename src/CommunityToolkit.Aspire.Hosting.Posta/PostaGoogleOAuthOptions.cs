using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Posta;

/// <summary>
/// Parameter-based configuration options for Posta Google OAuth login.
/// </summary>
public sealed class PostaGoogleOAuthOptions
{
    /// <summary>
    /// Gets or sets the Google OAuth client ID for SSO login.
    /// </summary>
    public IResourceBuilder<ParameterResource>? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the Google OAuth client secret for SSO login.
    /// </summary>
    public IResourceBuilder<ParameterResource>? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the OAuth callback base URL.
    /// </summary>
    public IResourceBuilder<ParameterResource>? CallbackUrl { get; set; }
}
