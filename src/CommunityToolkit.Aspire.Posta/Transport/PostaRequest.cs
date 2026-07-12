namespace CommunityToolkit.Aspire.Posta.Transport;

/// <summary>Arguments for invoking a Posta endpoint.</summary>
public sealed class PostaRequest
{
    /// <summary>Gets path parameter values without braces, for example <c>id</c>.</summary>
    public IReadOnlyDictionary<string, object?> PathParameters { get; init; } = new Dictionary<string, object?>();

    /// <summary>Gets query string values. Collections are emitted as repeated keys.</summary>
    public IReadOnlyDictionary<string, object?> Query { get; init; } = new Dictionary<string, object?>();

    /// <summary>Gets the object serialized as JSON.</summary>
    public object? Body { get; init; }

    /// <summary>Gets custom HTTP content for streaming or multipart requests.</summary>
    public HttpContent? Content { get; init; }

    /// <summary>Gets a per-request bearer credential overriding configured credentials.</summary>
    public string? BearerToken { get; init; }

    /// <summary>Gets additional request headers.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
}