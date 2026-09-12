// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using CommunityToolkit.Aspire.Hosting.GlitchTip.Management;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip;

internal static class GlitchTipLocalBootstrap
{
    private static readonly string[] TokenScopes =
    [
        "org:read", "org:write", "team:read", "team:write", "project:read", "project:write",
        "project:releases", "event:read", "event:write", "member:read"
    ];

    internal static async Task<string> EnsureAsync(Uri instance, string email, string password, string organization, string team,
        CancellationToken cancellationToken, HttpMessageHandler? messageHandler = null, CookieContainer? cookieContainer = null)
    {
        GlitchTipManagementClient.ValidateSlug(organization, nameof(organization));
        GlitchTipManagementClient.ValidateSlug(team, nameof(team));
        var cookies = cookieContainer ?? new CookieContainer();
        using var http = new HttpClient(messageHandler ?? new HttpClientHandler
        {
            CookieContainer = cookies,
            AllowAutoRedirect = false
        })
        { BaseAddress = instance, Timeout = TimeSpan.FromSeconds(30) };
        try
        {
            return await BootstrapAsync().ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            throw new DistributedApplicationException("GlitchTip local bootstrap could not reach the server. Check local resource logs; no authentication response is included.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DistributedApplicationException("GlitchTip local bootstrap timed out. Check local resource logs.");
        }
        catch (JsonException)
        {
            throw new DistributedApplicationException("GlitchTip local bootstrap received invalid JSON. Check the selected server version.");
        }

        async Task<string> BootstrapAsync()
        {
            using var session = await http.GetAsync("_allauth/browser/v1/auth/session", cancellationToken).ConfigureAwait(false);
            var csrf = GetCsrf();
            using var signup = await PostAsync("_allauth/browser/v1/auth/signup", new { email, password }, csrf).ConfigureAwait(false);
            if (!signup.IsSuccessStatusCode)
            {
                if (signup.StatusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict))
                {
                    EnsureSuccess(signup, "local signup");
                }
                using var login = await PostAsync("_allauth/browser/v1/auth/login", new { email, password }, GetCsrf()).ConfigureAwait(false);
                EnsureSuccess(login, "local login");
            }

            // Django rotates this cookie after login. Never log cookies or token bodies.
            csrf = GetCsrf();
            string? token = null;
            Uri? nextPage = new(instance, "api/0/api-tokens/");
            var visited = new HashSet<Uri>();
            while (nextPage is not null && token is null)
            {
                if (!visited.Add(nextPage) || visited.Count > 1000)
                {
                    throw new DistributedApplicationException("GlitchTip local token listing returned cyclic or excessive pagination.");
                }
                using var tokenList = await http.GetAsync(nextPage, cancellationToken).ConfigureAwait(false);
                EnsureSuccess(tokenList, "list local tokens");
                using var tokens = await ReadJsonAsync(tokenList, cancellationToken).ConfigureAwait(false);
                foreach (var item in tokens.RootElement.EnumerateArray())
                {
                    if (item.GetProperty("label").GetString() == "aspire-provisioner" &&
                        item.TryGetProperty("scopes", out var scopes) &&
                        TokenScopes.All(required => scopes.EnumerateArray().Any(scope => scope.GetString() == required)))
                    {
                        token = item.GetProperty("token").GetString();
                        if (!string.IsNullOrEmpty(token))
                        {
                            break;
                        }
                    }
                }
                nextPage = NextPage(tokenList, instance);
            }
            if (string.IsNullOrEmpty(token))
            {
                using var response = await PostAsync("api/0/api-tokens/", new
                {
                    label = "aspire-provisioner",
                    scopes = TokenScopes
                }, csrf).ConfigureAwait(false);
                EnsureSuccess(response, "create local token");
                using var body = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false);
                token = body.RootElement.GetProperty("token").GetString();
            }
            if (string.IsNullOrEmpty(token))
            {
                throw new DistributedApplicationException("GlitchTip local bootstrap returned an empty management token.");
            }
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            // Keep CSRF valid even if the server selects session authentication first.
            http.DefaultRequestHeaders.Add("X-CSRFToken", csrf);
            using var org = await http.GetAsync($"api/0/organizations/{organization}/", cancellationToken).ConfigureAwait(false);
            if (org.StatusCode == HttpStatusCode.NotFound)
            {
                using var created = await http.PostAsJsonAsync("api/0/organizations/", new
                {
                    name = organization == "aspire" ? "Aspire" : organization,
                    slug = organization
                }, cancellationToken).ConfigureAwait(false);
                EnsureSuccess(created, "create local organization");
                using var result = await ReadJsonAsync(created, cancellationToken).ConfigureAwait(false);
                VerifySlug(result, organization);
            }
            else
            {
                EnsureSuccess(org, "resolve local organization");
            }
            using var existingTeam = await http.GetAsync($"api/0/teams/{organization}/{team}/", cancellationToken).ConfigureAwait(false);
            if (existingTeam.StatusCode == HttpStatusCode.NotFound)
            {
                using var created = await http.PostAsJsonAsync($"api/0/organizations/{organization}/teams/", new { slug = team }, cancellationToken).ConfigureAwait(false);
                EnsureSuccess(created, "create local team");
                using var result = await ReadJsonAsync(created, cancellationToken).ConfigureAwait(false);
                VerifySlug(result, team);
            }
            else
            {
                EnsureSuccess(existingTeam, "resolve local team");
            }
            return token;
        }

        string GetCsrf() => cookies.GetCookies(instance)["csrftoken"]?.Value
            ?? throw new DistributedApplicationException("GlitchTip local bootstrap did not receive its CSRF cookie.");

        async Task<HttpResponseMessage> PostAsync(string path, object body, string csrfToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.Add("X-CSRFToken", csrfToken);
            return await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }

    private static Uri? NextPage(HttpResponseMessage response, Uri instance)
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
            if (!link.StartsWith('<') || !link.EndsWith('>') || !Uri.TryCreate(instance, link[1..^1], out var next) ||
                next.Scheme != instance.Scheme || next.Host != instance.Host || next.Port != instance.Port ||
                next.UserInfo.Length != 0 || next.AbsolutePath != new Uri(instance, "api/0/api-tokens/").AbsolutePath)
            {
                throw new DistributedApplicationException("GlitchTip returned an invalid local token pagination URL. Session credentials were not forwarded.");
            }
            return next;
        }
        return null;
    }

    private static void VerifySlug(JsonDocument response, string expected)
    {
        if (response.RootElement.GetProperty("slug").GetString() != expected)
        {
            throw new DistributedApplicationException("GlitchTip local bootstrap did not preserve the requested organization or team slug.");
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new DistributedApplicationException($"GlitchTip {operation} failed (HTTP {(int)response.StatusCode}). Check the local server logs.");
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
}