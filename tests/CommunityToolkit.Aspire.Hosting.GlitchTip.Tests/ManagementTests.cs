// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CommunityToolkit.Aspire.Hosting.GlitchTip.Management;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace CommunityToolkit.Aspire.Hosting.GlitchTip.Tests;

public class ManagementTests
{
    private const string Dsn = "https://11111111111111111111111111111111@glitchtip.example/42";

    [Fact]
    public async Task ExistingProjectIgnoresInitialTeamAndResolvesDsnAgain()
    {
        using var handler = new ScriptedHandler();
        for (var i = 0; i < 2; i++)
        {
            handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", Project());
            handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/keys/", Keys());
        }
        using var client = CreateClient(handler);
        Assert.Equal(Dsn, (await client.EnsureProjectAsync("org", "missing-team", "stack", null)).Dsn);
        Assert.Equal(Dsn, (await client.EnsureProjectAsync("org", "another-team", "stack", null)).Dsn);
        handler.AssertComplete();
    }

    [Fact]
    public async Task CreatesExactSlugWithSeparateDisplayNameAndDedicatedKey()
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", "{}", HttpStatusCode.NotFound);
        handler.Expect(HttpMethod.Post, "/api/0/teams/org/team/projects/", Project("stack"), HttpStatusCode.Created,
            payload =>
            {
                Assert.Equal("stack", payload!["slug"]!.GetValue<string>());
                Assert.Equal("stack", payload["name"]!.GetValue<string>());
            });
        handler.Expect(HttpMethod.Put, "/api/0/projects/org/stack/", Project("Friendly name"), assertBody: payload =>
        {
            Assert.Equal("Friendly name", payload!["name"]!.GetValue<string>());
            Assert.Equal("stack", payload["slug"]!.GetValue<string>());
        });
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/keys/", "[]");
        handler.Expect(HttpMethod.Post, "/api/0/projects/org/stack/keys/", Key(), HttpStatusCode.Created,
            payload => Assert.Equal("aspire", payload!["name"]!.GetValue<string>()));
        using var client = CreateClient(handler);
        var project = await client.EnsureProjectAsync("org", "team", "stack", "Friendly name");
        Assert.Equal("42", project.Id);
        Assert.Equal("stack", project.Slug);
        handler.AssertComplete();
    }

    [Fact]
    public async Task DisplayNameIsReconciledWithoutChangingTeam()
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", Project("Old"));
        handler.Expect(HttpMethod.Put, "/api/0/projects/org/stack/", Project("New"), assertBody: payload =>
        {
            Assert.Equal(2, payload!.AsObject().Count);
            Assert.Equal("New", payload["name"]!.GetValue<string>());
            Assert.Equal("stack", payload["slug"]!.GetValue<string>());
        });
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/keys/", Keys());
        using var client = CreateClient(handler);
        await client.EnsureProjectAsync("org", "team", "stack", "New");
        handler.AssertComplete();
    }

    [Fact]
    public async Task CreationConflictRereadsExactProject()
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", "{}", HttpStatusCode.NotFound);
        handler.Expect(HttpMethod.Post, "/api/0/teams/org/team/projects/", "{}", HttpStatusCode.Conflict);
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", Project());
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/keys/", Keys());
        using var client = CreateClient(handler);
        await client.EnsureProjectAsync("org", "team", "stack", null);
        handler.AssertComplete();
    }

    [Fact]
    public async Task SuffixedCreationNeverBecomesTheStackProject()
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", "{}", HttpStatusCode.NotFound);
        handler.Expect(HttpMethod.Post, "/api/0/teams/org/team/projects/", Project().Replace("stack", "stack-1"), HttpStatusCode.Created);
        using var client = CreateClient(handler);
        var error = await Assert.ThrowsAsync<GlitchTipManagementException>(() => client.EnsureProjectAsync("org", "team", "stack", null));
        Assert.Contains("slug", error.Message);
        handler.AssertComplete();
    }

    [Fact]
    public async Task ForbiddenLookupDoesNotAttemptCreationAndRedactsResponse()
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", "private-response-token", HttpStatusCode.Forbidden);
        using var client = CreateClient(handler);
        var error = await Assert.ThrowsAsync<GlitchTipManagementException>(() => client.EnsureProjectAsync("org", "team", "stack", null));
        Assert.Contains("403", error.Message);
        Assert.DoesNotContain("private-response-token", error.ToString());
        Assert.DoesNotContain("private-api-token", error.ToString());
        handler.AssertComplete();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectsActiveNamedKeyAcrossPagesAndHonorsTerminalLink(bool releasedSetWrapper)
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", Project());
        string Wrap(string value) => releasedSetWrapper ? "{'" + value + "'}" : value;
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/keys/",
            "[" + Key(name: "unrelated") + "," + Key(active: false) + "]",
            link: Wrap("<https://glitchtip.example/api/0/projects/org/stack/keys/?cursor=next>; rel=\"next\"; results=\"true\""));
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/keys/?cursor=next", Keys(),
            link: Wrap("<https://glitchtip.example/api/0/projects/org/stack/keys/>; rel=\"next\"; results=\"false\""));
        using var client = CreateClient(handler);
        Assert.Equal(Dsn, (await client.EnsureProjectAsync("org", "team", "stack", null)).Dsn);
        handler.AssertComplete();
    }

    [Fact]
    public async Task InactiveNamedKeyCreatesReplacementAndDoesNotModifyOldKeys()
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", Project());
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/keys/", "[" + Key(active: false) + "]");
        handler.Expect(HttpMethod.Post, "/api/0/projects/org/stack/keys/", Key(), HttpStatusCode.Created);
        using var client = CreateClient(handler);
        await client.EnsureProjectAsync("org", "team", "stack", null);
        handler.AssertComplete();
    }

    [Fact]
    public async Task PaginationNeverForwardsTokenToAnotherServer()
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", Project());
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/keys/", "[]",
            link: "<https://attacker.example/api/0/keys/>; rel=\"next\"; results=\"true\"");
        using var client = CreateClient(handler);
        var error = await Assert.ThrowsAsync<GlitchTipManagementException>(() => client.EnsureProjectAsync("org", "team", "stack", null));
        Assert.Contains("Credentials were not forwarded", error.Message);
        handler.AssertComplete();
    }

    [Fact]
    public async Task TransientReadsAreRetriedButMalformedResponsesAreNot()
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", "{}", HttpStatusCode.ServiceUnavailable);
        handler.Expect(HttpMethod.Get, "/api/0/projects/org/stack/", "not-json");
        using var client = CreateClient(handler);
        var error = await Assert.ThrowsAsync<GlitchTipManagementException>(() => client.EnsureProjectAsync("org", "team", "stack", null));
        Assert.Contains("invalid JSON", error.Message);
        handler.AssertComplete();
    }

    [Fact]
    public async Task BasePathIsPreserved()
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/glitchtip/api/0/projects/org/stack/", Project());
        handler.Expect(HttpMethod.Get, "/glitchtip/api/0/projects/org/stack/keys/", Keys());
        using var client = new GlitchTipManagementClient(new Uri("https://glitchtip.example/glitchtip"), "private-api-token", handler);
        await client.EnsureProjectAsync("org", "team", "stack", null);
        handler.AssertComplete();
    }

    [Theory]
    [InlineData("http://web:80/health")]
    [InlineData("https://internal-api/health")]
    public async Task BareMonitorHostnamesFailBeforeManagementRequests(string url)
    {
        using var handler = new ScriptedHandler();
        using var client = CreateClient(handler);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => client.ReconcileMonitorsAsync(
            "org", "42", "production", [new("web:http:/health", url, 60, 20, 200)]));
        Assert.Contains("WithGlitchTipMonitorUrl", error.Message);
        handler.AssertComplete();
    }
    [Fact]
    public async Task ReconcilesOnlyCurrentProjectAndEnvironmentPreservingUndeclaredFields()
    {
        var currentName = GlitchTipManagementClient.GetMonitorName("42", "production", "web:http:/health");
        var staleName = GlitchTipManagementClient.GetMonitorName("42", "production", "retired:http:/health");
        var otherEnvironment = GlitchTipManagementClient.GetMonitorName("42", "staging", "web:http:/health");
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/organizations/org/monitors/", new JsonArray(
            Monitor("1", currentName, "https://old.example/health", expectedBody: "still important", confirmationThreshold: 3),
            Monitor("2", staleName, "https://old.example/health"),
            Monitor("3", otherEnvironment, "https://staging.example/health"),
            Monitor("4", "Manually managed", "https://manual.example/health"),
            Monitor("5", staleName, "https://other.example/health", projectId: "99")).ToJsonString());
        handler.Expect(HttpMethod.Put, "/api/0/organizations/org/monitors/1/",
            Monitor("1", currentName, "https://new.example/health", expectedBody: "still important", confirmationThreshold: 3).ToJsonString(),
            assertBody: payload =>
            {
                Assert.Equal("still important", payload!["expectedBody"]!.GetValue<string>());
                Assert.Equal(3, payload["confirmationThreshold"]!.GetValue<int>());
                Assert.Equal("42", payload["project"]!.GetValue<string>());
                Assert.Equal("https://new.example/health", payload["url"]!.GetValue<string>());
            });
        handler.Expect(HttpMethod.Delete, "/api/0/organizations/org/monitors/2/", "", HttpStatusCode.NoContent);
        using var client = CreateClient(handler);
        await client.ReconcileMonitorsAsync("org", "42", "production", [new("web:http:/health", "https://new.example/health")]);
        handler.AssertComplete();
    }

    [Fact]
    public async Task UnchangedMonitorRequiresNoMutation()
    {
        var name = GlitchTipManagementClient.GetMonitorName("42", "production", "web:http:/health");
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/organizations/org/monitors/",
            new JsonArray(Monitor("1", name, "https://example.test/health")).ToJsonString());
        using var client = CreateClient(handler);
        await client.ReconcileMonitorsAsync("org", "42", "production", [new("web:http:/health", "https://example.test/health")]);
        handler.AssertComplete();
    }

    [Fact]
    public async Task FailedMonitorCreationDoesNotDeleteObsoleteMonitors()
    {
        var stale = GlitchTipManagementClient.GetMonitorName("42", "production", "retired");
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, "/api/0/organizations/org/monitors/", new JsonArray(Monitor("1", stale, "https://old.example/health")).ToJsonString());
        handler.Expect(HttpMethod.Post, "/api/0/organizations/org/monitors/", "private-content", HttpStatusCode.ServiceUnavailable);
        using var client = CreateClient(handler);
        await Assert.ThrowsAsync<GlitchTipManagementException>(() => client.ReconcileMonitorsAsync("org", "42", "production", [new("new", "https://new.example/health")]));
        handler.AssertComplete();
    }

    [Fact]
    public async Task DuplicateMonitorIdentityFailsBeforeHttp()
    {
        using var handler = new ScriptedHandler();
        using var client = CreateClient(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => client.ReconcileMonitorsAsync("org", "42", "production",
            [new("same", "https://one.example/health"), new("same", "https://two.example/health")]));
        handler.AssertComplete();
    }

    [Theory]
    [InlineData("stack/name")]
    [InlineData("a slug")]
    [InlineData("é")]
    [InlineData("Stack")]
    [InlineData("new")]
    [InlineData("stack--name")]
    [InlineData("-stack")]
    [InlineData("stack-")]
    [InlineData("_stack")]
    [InlineData("stack_")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task InvalidSlugFailsBeforeHttp(string slug)
    {
        using var handler = new ScriptedHandler();
        using var client = CreateClient(handler);
        await Assert.ThrowsAsync<ArgumentException>(() => client.EnsureProjectAsync("org", "team", slug, null));
        handler.AssertComplete();
    }

    [Theory]
    [InlineData("a")]
    [InlineData("1")]
    [InlineData("stack_1-test")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task CanonicalSlugIsPreservedAtItsValidBoundaries(string slug)
    {
        using var handler = new ScriptedHandler();
        handler.Expect(HttpMethod.Get, $"/api/0/projects/org/{slug}/", Project().Replace("stack", slug));
        handler.Expect(HttpMethod.Get, $"/api/0/projects/org/{slug}/keys/", Keys());
        using var client = CreateClient(handler);
        Assert.Equal(slug, (await client.EnsureProjectAsync("org", "team", slug, null)).Slug);
        handler.AssertComplete();
    }
    private static GlitchTipManagementClient CreateClient(HttpMessageHandler handler) =>
        new(new Uri("https://glitchtip.example"), "private-api-token", handler);

    private static string Project(string name = "stack") => new JsonObject { ["id"] = "42", ["slug"] = "stack", ["name"] = name }.ToJsonString();

    private static string Key(string name = "aspire", bool? active = null)
    {
        // Released 6.2.6 responses omit active state; explicit values model a server
        // that exposes the property so inactive keys still exercise replacement.
        var key = new JsonObject
        {
            ["id"] = "11111111-1111-1111-1111-111111111111",
            ["name"] = name,
            ["label"] = name,
            ["dsn"] = new JsonObject { ["public"] = Dsn }
        };
        if (active.HasValue)
        {
            key["isActive"] = active.Value;
        }
        return key.ToJsonString();
    }

    private static string Keys() => "[" + Key() + "]";

    private static JsonObject Monitor(string id, string name, string url, string projectId = "42", string expectedBody = "", int confirmationThreshold = 1) => new()
    {
        ["id"] = int.Parse(id),
        ["name"] = name,
        ["projectID"] = projectId,
        ["monitorType"] = "GET",
        ["url"] = url,
        ["interval"] = 60,
        ["timeout"] = 20,
        ["expectedStatus"] = 200,
        ["expectedBody"] = expectedBody,
        ["confirmationThreshold"] = confirmationThreshold
    };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _steps = new();

        internal void Expect(HttpMethod method, string path, string responseBody, HttpStatusCode status = HttpStatusCode.OK,
            Action<JsonNode?>? assertBody = null, string? link = null)
        {
            _steps.Enqueue(async request =>
            {
                Assert.Equal(method, request.Method);
                Assert.Equal(path, request.RequestUri!.PathAndQuery);
                Assert.Equal("private-api-token", request.Headers.Authorization!.Parameter);
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