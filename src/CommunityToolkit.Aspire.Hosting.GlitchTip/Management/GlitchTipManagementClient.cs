// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Management;

/// <summary>
/// Implements the released GlitchTip 6.2.6 management contract. Never caches a DSN.
/// </summary>
internal sealed class GlitchTipManagementClient : IDisposable
{
    private const string KeyName = "aspire";
    private readonly HttpClient _http;
    private readonly Uri _baseUri;

    internal GlitchTipManagementClient(Uri baseUri, string apiToken, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiToken);
        if (!baseUri.IsAbsoluteUri || baseUri.Scheme is not ("http" or "https") ||
            baseUri.UserInfo.Length != 0 || baseUri.Query.Length != 0 || baseUri.Fragment.Length != 0)
        {
            throw new ArgumentException("GlitchTip requires an absolute HTTP(S) instance URL without credentials, query, or fragment.", nameof(baseUri));
        }

        _baseUri = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/");
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = _baseUri,
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
    }

    internal async Task<GlitchTipProject> EnsureProjectAsync(
        string organizationSlug,
        string initialTeamSlug,
        string projectSlug,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        ValidateSlug(organizationSlug, nameof(organizationSlug));
        ValidateSlug(initialTeamSlug, nameof(initialTeamSlug));
        ValidateProjectSlug(projectSlug);
        if (displayName is not null && (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 64))
        {
            throw new ArgumentException("A GlitchTip project display name must contain 1 to 64 characters.", nameof(displayName));
        }

        var path = $"api/0/projects/{organizationSlug}/{projectSlug}/";
        var project = await GetOptionalAsync(path, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            // The released AutoSlugField derives the initial slug from the name.
            // Apply a friendly display name only after exact identity is verified.
            using var create = await SendAsync(HttpMethod.Post,
                $"api/0/teams/{organizationSlug}/{initialTeamSlug}/projects/",
                new JsonObject { ["name"] = projectSlug, ["slug"] = projectSlug },
                cancellationToken).ConfigureAwait(false);
            if (create.IsSuccessStatusCode)
            {
                project = await ReadObjectAsync(create, cancellationToken).ConfigureAwait(false);
                VerifyProject(project, projectSlug);
            }
            else if (create.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest || IsTransient(create.StatusCode))
            {
                // A concurrent creation or interrupted response may already have committed.
                // Re-read exact identity; never retry a non-idempotent create blindly.
                project = await GetOptionalAsync(path, cancellationToken).ConfigureAwait(false);
                if (project is null)
                {
                    ThrowStatus(create);
                }
            }
            else
            {
                ThrowStatus(create);
            }
        }

        VerifyProject(project!, projectSlug);
        if (displayName is not null && StringValue(project!, "name") != displayName)
        {
            using var update = await SendAsync(HttpMethod.Put, path,
                new JsonObject { ["name"] = displayName, ["slug"] = projectSlug }, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(update);
            project = await ReadObjectAsync(update, cancellationToken).ConfigureAwait(false);
            VerifyProject(project, projectSlug);
        }

        var projectId = RequiredString(project!, "id");
        var keyPath = path + "keys/";
        var keys = await GetAllAsync(keyPath, cancellationToken).ConfigureAwait(false);
        var key = FindActiveKey(keys);
        if (key is null)
        {
            using var create = await SendAsync(HttpMethod.Post, keyPath,
                new JsonObject { ["name"] = KeyName }, cancellationToken).ConfigureAwait(false);
            if (create.IsSuccessStatusCode)
            {
                key = await ReadObjectAsync(create, cancellationToken).ConfigureAwait(false);
                if (!IsActiveAspireKey(key))
                {
                    throw ContractError("The created reporting key was not active or did not preserve its requested name.");
                }
            }
            else if (create.StatusCode == HttpStatusCode.Conflict || IsTransient(create.StatusCode))
            {
                key = FindActiveKey(await GetAllAsync(keyPath, cancellationToken).ConfigureAwait(false));
                if (key is null)
                {
                    ThrowStatus(create);
                }
            }
            else
            {
                ThrowStatus(create);
            }
        }

        var dsn = key!["dsn"]?["public"]?.GetValue<string>();
        if (!Uri.TryCreate(dsn, UriKind.Absolute, out var dsnUri) ||
            dsnUri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(dsnUri.UserInfo) ||
            dsnUri.Segments.LastOrDefault()?.TrimEnd('/') != projectId)
        {
            throw ContractError("GlitchTip returned an invalid reporting DSN for the resolved project. Check the server's GLITCHTIP_URL setting.");
        }

        return new GlitchTipProject(projectId, projectSlug, dsn!);
    }

    internal async Task ReconcileMonitorsAsync(
        string organizationSlug,
        string projectId,
        string environment,
        IReadOnlyList<GlitchTipMonitorDefinition> monitors,
        CancellationToken cancellationToken = default)
    {
        ValidateSlug(organizationSlug, nameof(organizationSlug));
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentNullException.ThrowIfNull(monitors);
        var desired = new Dictionary<string, GlitchTipMonitorDefinition>(StringComparer.Ordinal);
        foreach (var monitor in monitors)
        {
            ValidateMonitor(monitor);
            if (!desired.TryAdd(GetMonitorName(projectId, environment, monitor.Identity), monitor))
            {
                throw new ArgumentException("GlitchTip HTTP health-check identities must be unique within the stack.", nameof(monitors));
            }
        }

        var path = $"api/0/organizations/{organizationSlug}/monitors/";
        var scope = GetMonitorPrefix(projectId, environment);
        var existing = (await GetAllAsync(path, cancellationToken).ConfigureAwait(false))
            .Where(item => StringValue(item, "projectID") == projectId &&
                StringValue(item, "name")?.StartsWith(scope, StringComparison.Ordinal) == true)
            .ToList();
        if (existing.GroupBy(item => RequiredString(item, "name"), StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            throw ContractError("Duplicate Aspire-managed monitors exist. Remove the duplicates before reconciling.");
        }

        foreach (var (name, definition) in desired)
        {
            var current = existing.SingleOrDefault(item => StringValue(item, "name") == name);
            var payload = new JsonObject
            {
                ["name"] = name,
                ["project"] = projectId,
                ["monitorType"] = "GET",
                ["url"] = definition.Url,
                ["interval"] = definition.IntervalSeconds,
                ["timeout"] = definition.TimeoutSeconds,
                ["expectedStatus"] = definition.ExpectedStatusCode,
                // GlitchTip PUT replaces every input field. Preserve undeclared settings.
                ["expectedBody"] = current?["expectedBody"]?.DeepClone() ?? JsonValue.Create(""),
                ["confirmationThreshold"] = current?["confirmationThreshold"]?.DeepClone() ?? JsonValue.Create(1)
            };
            if (current is not null && payload.All(field => JsonNode.DeepEquals(field.Value,
                field.Key == "project" ? JsonValue.Create(StringValue(current, "projectID")) : current[field.Key])))
            {
                continue;
            }

            using var response = await SendAsync(current is null ? HttpMethod.Post : HttpMethod.Put,
                current is null ? path : path + Uri.EscapeDataString(RequiredString(current, "id")) + "/",
                payload, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response);
            var result = await ReadObjectAsync(response, cancellationToken).ConfigureAwait(false);
            if (StringValue(result, "name") != name || StringValue(result, "projectID") != projectId)
            {
                throw ContractError("GlitchTip did not preserve the managed monitor identity.");
            }
        }

        // Only clean up once every desired monitor has been applied successfully.
        foreach (var obsolete in existing.Where(item => !desired.ContainsKey(RequiredString(item, "name"))))
        {
            using var response = await SendAsync(HttpMethod.Delete,
                path + Uri.EscapeDataString(RequiredString(obsolete, "id")) + "/", null, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                EnsureSuccess(response);
            }
        }
    }

    internal static string GetMonitorName(string projectId, string environment, string identity)
    {
        return GetMonitorPrefix(projectId, environment) + Hash(identity);
    }

    private static string GetMonitorPrefix(string projectId, string environment)
    {
        return "aspire:v1:" + Hash(JsonSerializer.Serialize(new[] { projectId, environment })) + ":";
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static void ValidateSlug(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 50 || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
        {
            throw new ArgumentException("GlitchTip slugs must contain 1 to 50 ASCII letters, digits, hyphens, or underscores; values are not normalized.", parameterName);
        }
    }

    internal static void ValidateProjectSlug(string projectSlug)
    {
        ValidateSlug(projectSlug, nameof(projectSlug));
        // The released server applies Django slugify on create. Reject every input
        // it would normalize, including its reserved creation route name.
        if (projectSlug.Any(char.IsAsciiLetterUpper) ||
            !char.IsAsciiLetterOrDigit(projectSlug[0]) || !char.IsAsciiLetterOrDigit(projectSlug[^1]) ||
            projectSlug.Contains("--", StringComparison.Ordinal) || projectSlug == "new")
        {
            throw new ArgumentException("GlitchTip project slugs must be lowercase, start and end with a letter or digit, contain no consecutive hyphens, and must not be 'new'. Use the display name for human-readable text.", nameof(projectSlug));
        }
    }
    private static void ValidateMonitor(GlitchTipMonitorDefinition monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentException.ThrowIfNullOrWhiteSpace(monitor.Identity);
        if (!Uri.TryCreate(monitor.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || monitor.Url.Length > 2000)
        {
            throw new ArgumentException("GlitchTip monitor URLs must be absolute HTTP(S) addresses without credentials or fragments.", nameof(monitor));
        }
        // GlitchTip 6.2.6 applies Django URLValidator before its private-address policy.
        // Bare Compose service names are rejected even when private addresses are allowed.
        if (uri.HostNameType == UriHostNameType.Dns && !uri.IdnHost.Contains('.') &&
            !string.Equals(uri.IdnHost, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("GlitchTip monitor URLs require a fully qualified hostname or IP address. Bare Compose service names are not accepted. Supply the complete reachable address with WithGlitchTipMonitorUrl.", nameof(monitor));
        }
        if (monitor.IntervalSeconds is < 1 or > 86400 || monitor.TimeoutSeconds is < 1 or > 60 || monitor.ExpectedStatusCode is < 100 or > 599)
        {
            throw new ArgumentException("GlitchTip monitor interval must be 1–86400 seconds, timeout 1–60 seconds, and expected status 100–599.", nameof(monitor));
        }
    }

    private static void VerifyProject(JsonObject project, string slug)
    {
        if (StringValue(project, "slug") != slug)
        {
            throw ContractError("GlitchTip did not preserve the requested project slug. A concurrent create may have produced a suffixed project; resolve that conflict before retrying.");
        }
        _ = RequiredString(project, "id");
    }

    private static JsonObject? FindActiveKey(IEnumerable<JsonObject> keys) => keys
        .Where(IsActiveAspireKey)
        .OrderBy(key => StringValue(key, "dateCreated"), StringComparer.Ordinal)
        .ThenBy(key => StringValue(key, "id"), StringComparer.Ordinal)
        .FirstOrDefault();

    private static bool IsActiveAspireKey(JsonObject key)
    {
        if ((StringValue(key, "name") ?? StringValue(key, "label")) != KeyName)
        {
            return false;
        }

        // GlitchTip 6.2.6 omits active state from its key API and cannot update it.
        // Reuse its named key; honor explicit inactive state on servers that expose it.
        // Deleting this key is the supported way to request a fresh reporting key.
        var active = key["isActive"] ?? key["is_active"];
        return active is null || active.GetValue<bool>();
    }

    private async Task<JsonObject?> GetOptionalAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        EnsureSuccess(response);
        return await ReadObjectAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<JsonObject>> GetAllAsync(string path, CancellationToken cancellationToken)
    {
        var result = new List<JsonObject>();
        var visited = new HashSet<Uri>();
        Uri? next = new(_baseUri, path);
        while (next is not null)
        {
            if (!visited.Add(next) || visited.Count > 1000)
            {
                throw ContractError("GlitchTip returned cyclic or excessive pagination.");
            }
            using var response = await SendAsync(HttpMethod.Get, next.AbsoluteUri, null, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response);
            var data = await ReadNodeAsync(response, cancellationToken).ConfigureAwait(false);
            if (data is not JsonArray array || array.Any(item => item is not JsonObject))
            {
                throw ContractError("GlitchTip returned an invalid paginated response.");
            }
            result.AddRange(array.Cast<JsonObject>());
            next = GetNextPage(response);
        }
        return result;
    }

    private Uri? GetNextPage(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var headers))
        {
            return null;
        }
        foreach (var part in GlitchTipPagination.SplitLinks(headers))
        {
            var segments = part.Split(';', StringSplitOptions.TrimEntries);
            if (!segments.Contains("rel=\"next\"", StringComparer.OrdinalIgnoreCase) ||
                segments.Contains("results=\"false\"", StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            var link = segments[0];
            if (!link.StartsWith('<') || !link.EndsWith('>') || !Uri.TryCreate(_baseUri, link[1..^1], out var uri))
            {
                throw ContractError("GlitchTip returned an invalid pagination link.");
            }
            ValidateRequestUri(uri);
            return uri;
        }
        return null;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, JsonObject? payload, CancellationToken cancellationToken)
    {
        var uri = new Uri(_baseUri, path);
        ValidateRequestUri(uri);
        const int maxAttempts = 3;
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, uri);
            if (payload is not null)
            {
                request.Content = JsonContent.Create(payload);
            }
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                if (method == HttpMethod.Post || attempt == maxAttempts - 1)
                {
                    throw ContractError("GlitchTip management could not reach the server. A create may have committed; retry the deployment to reconcile.");
                }
                await Task.Delay(TimeSpan.FromMilliseconds(100 * (1 << attempt)), cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw ContractError("GlitchTip management timed out. A create may have committed; retry the deployment to reconcile.");
            }
            if (method == HttpMethod.Post || !IsTransient(response.StatusCode) || attempt == maxAttempts - 1)
            {
                return response;
            }
            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(100 * (1 << attempt));
            response.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(delay.TotalMilliseconds, 0, 2000)), cancellationToken).ConfigureAwait(false);
        }
    }

    private void ValidateRequestUri(Uri uri)
    {
        if (uri.Scheme != _baseUri.Scheme || uri.Host != _baseUri.Host || uri.Port != _baseUri.Port ||
            uri.UserInfo.Length != 0 || !uri.AbsolutePath.StartsWith(_baseUri.AbsolutePath + "api/0/", StringComparison.Ordinal))
        {
            throw ContractError("GlitchTip returned a management URL outside the configured instance. Credentials were not forwarded.");
        }
    }

    private static bool IsTransient(HttpStatusCode status) => status is HttpStatusCode.RequestTimeout or
        HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            ThrowStatus(response);
        }
    }

    private static void ThrowStatus(HttpResponseMessage response) =>
        throw ContractError($"GlitchTip management returned HTTP {(int)response.StatusCode}. Check instance availability, token permissions, organization, and initial team. No response body or credentials are included.");

    private static GlitchTipManagementException ContractError(string message) => new(message);

    private static string? StringValue(JsonObject item, string property) => item[property]?.ToString();

    private static string RequiredString(JsonObject item, string property) =>
        !string.IsNullOrEmpty(StringValue(item, property)) ? StringValue(item, property)! : throw ContractError("GlitchTip returned an incomplete management response.");

    private static async Task<JsonNode> ReadNodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken).ConfigureAwait(false)
                ?? throw ContractError("GlitchTip returned an empty management response.");
        }
        catch (JsonException)
        {
            throw ContractError("GlitchTip returned an invalid JSON management response.");
        }
    }

    private static async Task<JsonObject> ReadObjectAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await ReadNodeAsync(response, cancellationToken).ConfigureAwait(false) as JsonObject
            ?? throw ContractError("GlitchTip returned an invalid management object.");

    public void Dispose() => _http.Dispose();
}