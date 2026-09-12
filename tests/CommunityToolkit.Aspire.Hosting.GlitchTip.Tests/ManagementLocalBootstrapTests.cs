// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Tests;

public class ManagementLocalBootstrapTests
{
    private static readonly Uri Instance = new("http://glitchtip.local/");
    private static readonly string[] Scopes =
    [
        "org:read", "org:write", "team:read", "team:write", "project:read", "project:write",
        "project:releases", "event:read", "event:write", "member:read"
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstStartupCreatesIdentityAndUsesRotatedCsrf(bool releasedSetWrapper)
    {
        var cookies = new CookieContainer();
        using var handler = new BootstrapHandler();
        handler.Expect(HttpMethod.Get, "/_allauth/browser/v1/auth/session", "{}", HttpStatusCode.Unauthorized,
            request => cookies.Add(Instance, new Cookie("csrftoken", "before-login")));
        handler.Expect(HttpMethod.Post, "/_allauth/browser/v1/auth/signup", "{}", assertRequest: request =>
        {
            Assert.Equal("before-login", Assert.Single(request.Headers.GetValues("X-CSRFToken")));
            cookies.Add(Instance, new Cookie("csrftoken", "after-login"));
        });
        handler.Expect(HttpMethod.Get, "/api/0/api-tokens/", "[]",
            link: releasedSetWrapper
                ? "{'<http://glitchtip.local/api/0/api-tokens/>; rel=\"previous\"; results=\"false\", <http://glitchtip.local/api/0/api-tokens/>; rel=\"next\"; results=\"false\"'}"
                : "<http://glitchtip.local/api/0/api-tokens/>; rel=\"next\"; results=\"false\"");
        handler.Expect(HttpMethod.Post, "/api/0/api-tokens/", "{\"token\":\"local-management-token\"}", HttpStatusCode.Created,
            request => Assert.Equal("after-login", Assert.Single(request.Headers.GetValues("X-CSRFToken"))),
            payload => Assert.Equal("aspire-provisioner", payload!["label"]!.GetValue<string>()));
        handler.Expect(HttpMethod.Get, "/api/0/organizations/aspire/", "{}", HttpStatusCode.NotFound);
        handler.Expect(HttpMethod.Post, "/api/0/organizations/", "{\"slug\":\"aspire\"}", HttpStatusCode.Created,
            request => Assert.Equal("local-management-token", request.Headers.Authorization!.Parameter),
            payload => Assert.Equal("Aspire", payload!["name"]!.GetValue<string>()));
        handler.Expect(HttpMethod.Get, "/api/0/teams/aspire/aspire/", "{}", HttpStatusCode.NotFound);
        handler.Expect(HttpMethod.Post, "/api/0/organizations/aspire/teams/", "{\"slug\":\"aspire\"}", HttpStatusCode.Created);
        var token = await GlitchTipLocalBootstrap.EnsureAsync(Instance, "admin@aspire.local", "private-password", "aspire", "aspire", default, handler, cookies);
        Assert.Equal("local-management-token", token);
        handler.AssertComplete();
    }

    [Fact]
    public async Task RestartLogsInAndReusesExistingTokenAcrossPages()
    {
        var cookies = new CookieContainer();
        using var handler = new BootstrapHandler();
        handler.Expect(HttpMethod.Get, "/_allauth/browser/v1/auth/session", "{}", HttpStatusCode.Unauthorized,
            request => cookies.Add(Instance, new Cookie("csrftoken", "csrf")));
        handler.Expect(HttpMethod.Post, "/_allauth/browser/v1/auth/signup", "{}", HttpStatusCode.BadRequest);
        handler.Expect(HttpMethod.Post, "/_allauth/browser/v1/auth/login", "{}");
        handler.Expect(HttpMethod.Get, "/api/0/api-tokens/", "[]",
            link: "<http://glitchtip.local/api/0/api-tokens/?cursor=next>; rel=\"next\"; results=\"true\"");
        handler.Expect(HttpMethod.Get, "/api/0/api-tokens/?cursor=next", new JsonArray(new JsonObject
        {
            ["label"] = "aspire-provisioner",
            ["token"] = "existing-token",
            ["scopes"] = new JsonArray([.. Scopes.Select(scope => (JsonNode?)JsonValue.Create(scope))])
        }).ToJsonString(), link: "<http://glitchtip.local/api/0/api-tokens/>; rel=\"next\"; results=\"false\"");
        handler.Expect(HttpMethod.Get, "/api/0/organizations/aspire/", "{\"slug\":\"aspire\"}");
        handler.Expect(HttpMethod.Get, "/api/0/teams/aspire/aspire/", "{\"slug\":\"aspire\"}");
        var token = await GlitchTipLocalBootstrap.EnsureAsync(Instance, "admin@aspire.local", "private-password", "aspire", "aspire", default, handler, cookies);
        Assert.Equal("existing-token", token);
        handler.AssertComplete();
    }

    [Fact]
    public async Task SignupServerFailureDoesNotFallThroughToLoginOrLeakResponse()
    {
        var cookies = new CookieContainer();
        using var handler = new BootstrapHandler();
        handler.Expect(HttpMethod.Get, "/_allauth/browser/v1/auth/session", "{}", HttpStatusCode.Unauthorized,
            request => cookies.Add(Instance, new Cookie("csrftoken", "csrf")));
        handler.Expect(HttpMethod.Post, "/_allauth/browser/v1/auth/signup", "private-password", HttpStatusCode.ServiceUnavailable);
        var error = await Assert.ThrowsAsync<DistributedApplicationException>(() => GlitchTipLocalBootstrap.EnsureAsync(
            Instance, "admin@aspire.local", "private-password", "aspire", "aspire", default, handler, cookies));
        Assert.DoesNotContain("private-password", error.ToString());
        Assert.Contains("503", error.Message);
        handler.AssertComplete();
    }

    [Fact]
    public void StorageScopeIsStableAndIsolatedByAppHostDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "glitchtip-worktree-one");
        Assert.Equal(GlitchTipLocalHosting.GetStorageScope(directory), GlitchTipLocalHosting.GetStorageScope(directory + Path.DirectorySeparatorChar));
        Assert.NotEqual(GlitchTipLocalHosting.GetStorageScope(directory), GlitchTipLocalHosting.GetStorageScope(directory + "-two"));
    }

    private sealed class BootstrapHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _steps = new();

        internal void Expect(HttpMethod method, string path, string responseBody, HttpStatusCode status = HttpStatusCode.OK,
            Action<HttpRequestMessage>? assertRequest = null, Action<JsonNode?>? assertBody = null, string? link = null)
        {
            _steps.Enqueue(async request =>
            {
                Assert.Equal(method, request.Method);
                Assert.Equal(path, request.RequestUri!.PathAndQuery);
                assertRequest?.Invoke(request);
                if (assertBody is not null)
                {
                    assertBody(await request.Content!.ReadFromJsonAsync<JsonNode>());
                }
                var response = new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
                if (link is not null)
                {
                    response.Headers.TryAddWithoutValidation("Link", link);
                }
                return response;
            });
        }

        internal void AssertComplete() => Assert.Empty(_steps);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.NotEmpty(_steps);
            return _steps.Dequeue()(request);
        }
    }
}