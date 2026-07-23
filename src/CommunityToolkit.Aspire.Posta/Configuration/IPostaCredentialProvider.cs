using CommunityToolkit.Aspire.Posta.Endpoints;

namespace CommunityToolkit.Aspire.Posta.Configuration;

/// <summary>Resolves bearer credentials for Posta requests.</summary>
public interface IPostaCredentialProvider
{
    /// <summary>Returns the credential for the requested authentication mode.</summary>
    ValueTask<string?> GetCredentialAsync(PostaAuthentication authentication, CancellationToken cancellationToken = default);
}