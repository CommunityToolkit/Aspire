using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Transport;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Posta.Clients;

/// <summary>
/// Base implementation used by Posta API section clients.
/// </summary>
public abstract class PostaSectionClient : IPostaSectionClient
{
    private readonly PostaTransport _transport;

    internal PostaSectionClient(PostaTransport transport)
    {
        _transport = transport;
    }

    /// <inheritdoc />
    public Task<TResponse?> SendAsync<TResponse>(PostaEndpoint endpoint, PostaRequest? request = null, CancellationToken cancellationToken = default)
    {
        return _transport.SendAsync<TResponse>(endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JsonDocument?> SendAsync(PostaEndpoint endpoint, PostaRequest? request = null, CancellationToken cancellationToken = default)
    {
        return _transport.SendAsync(endpoint, request, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PostaStreamResponse> SendStreamAsync(PostaEndpoint endpoint, PostaRequest? request = null, CancellationToken cancellationToken = default)
    {
        return _transport.SendStreamAsync(endpoint, request, cancellationToken);
    }
}