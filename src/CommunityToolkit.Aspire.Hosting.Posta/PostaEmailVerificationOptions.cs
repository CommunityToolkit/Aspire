using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Posta;

/// <summary>
/// Parameter-based configuration options for Posta email verification.
/// </summary>
public sealed class PostaEmailVerificationOptions
{
    /// <summary>
    /// Gets or sets whether users must verify their email address before sign-in.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Required { get; set; }

    /// <summary>
    /// Gets or sets whether permanently rejected recipients are added to the suppression list.
    /// </summary>
    public IResourceBuilder<ParameterResource>? AutoSuppressOnReject { get; set; }

    /// <summary>
    /// Gets or sets whether the email verification endpoint is enabled.
    /// </summary>
    public IResourceBuilder<ParameterResource>? Enabled { get; set; }

    /// <summary>
    /// Gets or sets how long address-level verification results are cached, in hours.
    /// </summary>
    public IResourceBuilder<ParameterResource>? CacheTtlHours { get; set; }

    /// <summary>
    /// Gets or sets how long domain MX lookups are cached, in hours.
    /// </summary>
    public IResourceBuilder<ParameterResource>? MxCacheTtlHours { get; set; }

    /// <summary>
    /// Gets or sets the per-user hourly cap on verification requests.
    /// </summary>
    public IResourceBuilder<ParameterResource>? RateHourly { get; set; }
}
