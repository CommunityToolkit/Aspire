using CommunityToolkit.Aspire.Posta.Endpoints;

namespace CommunityToolkit.Aspire.Posta.Configuration;

internal sealed class PostaCredentialProvider(PostaClientSettings settings) : IPostaCredentialProvider
{
    public ValueTask<string?> GetCredentialAsync(PostaAuthentication authentication, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(authentication switch
        {
            PostaAuthentication.None => null,
            PostaAuthentication.ApiKey => settings.ApiKey,
            PostaAuthentication.AccessToken => settings.AccessToken,
            _ => throw new ArgumentOutOfRangeException(nameof(authentication))
        });
    }
}