using CommunityToolkit.Aspire.Posta.Configuration;
using CommunityToolkit.Aspire.Posta.Endpoints;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Posta.Transport;

internal sealed class PostaTransport(HttpClient httpClient, IPostaCredentialProvider credentialProvider)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TResponse?> SendAsync<TResponse>(PostaEndpoint endpoint, PostaRequest? request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendCoreAsync(endpoint, request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength == 0 || response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return default;
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(s_jsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonDocument?> SendAsync(PostaEndpoint endpoint, PostaRequest? request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendCoreAsync(endpoint, request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength == 0 || response.StatusCode == System.Net.HttpStatusCode.NoContent)
        {
            return null;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<PostaStreamResponse> SendStreamAsync(PostaEndpoint endpoint, PostaRequest? request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await SendCoreAsync(endpoint, request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new PostaStreamResponse(response, stream);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(PostaEndpoint endpoint, PostaRequest? request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsImplemented)
        {
            throw new NotSupportedException($"The default Posta client cannot invoke '{endpoint.Path}': {endpoint.Documentation}");
        }

        request ??= new PostaRequest();
        string path = ExpandPath(endpoint.Path, request.PathParameters);
        path = AddQueryString(path, request.Query);

        using HttpRequestMessage message = new(endpoint.Method, path);
        if (request.Content is not null && request.Body is not null)
        {
            throw new ArgumentException("Specify either Body or Content, not both.", nameof(request));
        }

        if (request.Content is not null)
        {
            message.Content = request.Content;
        }
        else if (request.Body is not null)
        {
            message.Content = JsonContent.Create(request.Body, options: s_jsonOptions);
        }

        string? credential = request.BearerToken ?? await credentialProvider.GetCredentialAsync(endpoint.Authentication, cancellationToken).ConfigureAwait(false);
        if (endpoint.Authentication != PostaAuthentication.None)
        {
            if (string.IsNullOrWhiteSpace(credential))
            {
                throw new InvalidOperationException($"A credential for {endpoint.Authentication} is required by '{endpoint.Path}'.");
            }

            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
        }

        foreach ((string name, string value) in request.Headers)
        {
            if (!message.Headers.TryAddWithoutValidation(name, value))
            {
                message.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        HttpResponseMessage response = await httpClient.SendAsync(message, completionOption, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        string? responseBody = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var statusCode = response.StatusCode;
        response.Dispose();
        throw new PostaApiException(statusCode, responseBody, $"Posta returned HTTP {(int)statusCode} ({statusCode}) for {endpoint.Method} {path}.");
    }

    private static string ExpandPath(string template, IReadOnlyDictionary<string, object?> values)
    {
        string result = template;
        foreach ((string name, object? value) in values)
        {
            result = result.Replace("{" + name + "}", Uri.EscapeDataString(ConvertToString(value)), StringComparison.Ordinal);
        }

        if (result.Contains('{', StringComparison.Ordinal))
        {
            throw new ArgumentException($"Not all path parameters were supplied for '{template}'.", nameof(values));
        }

        return result;
    }

    private static string AddQueryString(string path, IReadOnlyDictionary<string, object?> query)
    {
        List<string> values = [];
        foreach ((string name, object? value) in query)
        {
            if (value is null)
            {
                continue;
            }

            if (value is System.Collections.IEnumerable enumerable and not string)
            {
                foreach (object? item in enumerable)
                {
                    if (item is not null)
                    {
                        values.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(ConvertToString(item))}");
                    }
                }
            }
            else
            {
                values.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(ConvertToString(value))}");
            }
        }

        return values.Count == 0 ? path : $"{path}?{string.Join('&', values)}";
    }

    private static string ConvertToString(object? value) => value switch
    {
        null => string.Empty,
        bool boolean => boolean ? "true" : "false",
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}