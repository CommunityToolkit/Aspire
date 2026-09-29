namespace CommunityToolkit.Aspire.Posta.Configuration;

/// <summary>Configuration used by the Posta HTTP client.</summary>
public sealed class PostaClientSettings
{
    /// <summary>Gets or sets the Posta server base address.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>Gets or sets a long-lived Posta API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets a JWT issued by the Posta login endpoint.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Gets or sets the request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}