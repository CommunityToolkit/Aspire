namespace CommunityToolkit.Aspire.Posta.Endpoints;

/// <summary>Describes a Posta API operation.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The relative path template.</param>
/// <param name="Authentication">The authentication required by the operation.</param>
/// <param name="IsImplemented">Whether the published OpenAPI contract is complete enough for the default transport.</param>
/// <param name="Documentation">An optional implementation note.</param>
public sealed record PostaEndpoint(
    HttpMethod Method,
    string Path,
    PostaAuthentication Authentication,
    bool IsImplemented = true,
    string? Documentation = null);

/// <summary>Authentication modes used by Posta.</summary>
public enum PostaAuthentication
{
    /// <summary>No authentication is sent.</summary>
    None,

    /// <summary>A long-lived API key is sent as a bearer credential.</summary>
    ApiKey,

    /// <summary>A JWT access token is sent as a bearer credential.</summary>
    AccessToken
}