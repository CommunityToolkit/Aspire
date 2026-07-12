using System.Net;

namespace CommunityToolkit.Aspire.Posta.Transport;

/// <summary>Represents a non-successful response returned by Posta.</summary>
public sealed class PostaApiException : HttpRequestException
{
    internal PostaApiException(HttpStatusCode statusCode, string? responseBody, string message)
        : base(message, null, statusCode)
    {
        ResponseBody = responseBody;
    }

    /// <summary>Gets the response body, when one was returned.</summary>
    public string? ResponseBody { get; }
}