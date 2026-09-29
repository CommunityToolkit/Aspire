using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Transport;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Posta.Clients;

/// <summary>
/// Base implementation used by Posta API section clients.
/// </summary>
internal abstract class PostaSectionClient(PostaTransport transport) : IPostaSectionClient
{
    /// <inheritdoc />
    public Task<TResponse?> SendAsync<TResponse>(PostaEndpoint endpoint, PostaRequest? request = null, CancellationToken cancellationToken = default)
    {
        return transport.SendAsync<TResponse>(endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JsonDocument?> SendAsync(PostaEndpoint endpoint, PostaRequest? request = null, CancellationToken cancellationToken = default)
    {
        return transport.SendAsync(endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PostaStreamResponse> SendStreamAsync(PostaEndpoint endpoint, PostaRequest? request = null, CancellationToken cancellationToken = default)
    {
        return transport.SendStreamAsync(endpoint, request, cancellationToken);
    }
}
