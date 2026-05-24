using Bitwarden.Sdk;

namespace CommunityToolkit.Aspire.Bitwarden.SecretManager;

/// <summary>
/// Settings used to configure a <see cref="BitwardenClient"/>.
/// </summary>
public sealed class BitwardenSecretManagerClientSettings
{
    /// <summary>
    /// Gets or sets the Bitwarden organization identifier.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the Bitwarden project identifier.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the Bitwarden access token.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Bitwarden API URL.
    /// </summary>
    public string ApiUrl { get; set; } = "https://api.bitwarden.com";

    /// <summary>
    /// Gets or sets the Bitwarden identity URL.
    /// </summary>
    public string IdentityUrl { get; set; } = "https://identity.bitwarden.com";

    /// <summary>
    /// Gets or sets the optional auth cache file path used by the Bitwarden SDK to persist the auth session across restarts.
    /// Set this to a persistent storage path (e.g. <c>/data/bitwarden/auth-cache</c>) to avoid re-authenticating on every start.
    /// Injected automatically by the AppHost integration via the <c>AuthCacheFile</c> configuration key when using <c>WithAuthCacheFile</c>.
    /// </summary>
    public string? AuthCacheFile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether health checks should be disabled.
    /// </summary>
    public bool DisableHealthChecks { get; set; }

    /// <summary>
    /// Gets or sets the optional health check timeout.
    /// </summary>
    public TimeSpan? HealthCheckTimeout { get; set; }
}