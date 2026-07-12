using System.Net;
using System.Net.Http.Headers;

namespace CommunityToolkit.Aspire.Posta.Transport;

/// <summary>Owns a streaming Posta response and its content stream.</summary>
public sealed class PostaStreamResponse : IDisposable, IAsyncDisposable
{
    private readonly HttpResponseMessage _response;

    internal PostaStreamResponse(HttpResponseMessage response, Stream stream)
    {
        _response = response;
        Stream = stream;
    }

    /// <summary>Gets the response content stream.</summary>
    public Stream Stream { get; }

    /// <summary>Gets the response status code.</summary>
    public HttpStatusCode StatusCode => _response.StatusCode;

    /// <summary>Gets response content headers.</summary>
    public HttpContentHeaders Headers => _response.Content.Headers;

    /// <inheritdoc />
    public void Dispose() => _response.Dispose();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _response.Dispose();
        return ValueTask.CompletedTask;
    }
}