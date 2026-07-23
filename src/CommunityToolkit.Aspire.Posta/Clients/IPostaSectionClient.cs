using CommunityToolkit.Aspire.Posta.Endpoints;
using CommunityToolkit.Aspire.Posta.Transport;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Posta.Clients;

/// <summary>Common operations exposed by each Posta API section.</summary>
public interface IPostaSectionClient
{
    /// <summary>Invokes an endpoint and deserializes its JSON response.</summary>
    Task<TResponse?> SendAsync<TResponse>(PostaEndpoint endpoint, PostaRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>Invokes an endpoint and returns its JSON response.</summary>
    Task<JsonDocument?> SendAsync(PostaEndpoint endpoint, PostaRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>Invokes an endpoint and returns a streaming response. Disposing the result disposes the HTTP response.</summary>
    Task<PostaStreamResponse> SendStreamAsync(PostaEndpoint endpoint, PostaRequest? request = null, CancellationToken cancellationToken = default);
}