using System.Net;
using System.Net.Http.Headers;

namespace CommunityToolkit.Aspire.Posta.Transport;

/// <summary>Owns a streaming Posta response and its content stream.</summary>
public sealed class PostaStreamResponse(HttpResponseMessage response, Stream stream) : IDisposable, IAsyncDisposable
{
    /// <summary>Gets the response content stream.</summary>
    public Stream Stream { get; } = stream;

    /// <summary>Gets the response status code.</summary>
    public HttpStatusCode StatusCode => response.StatusCode;

    /// <summary>Gets response content headers.</summary>
    public HttpContentHeaders Headers => response.Content.Headers;

    /// <inheritdoc />
    public void Dispose() => response.Dispose();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        response.Dispose();
        return ValueTask.CompletedTask;
    }
}
