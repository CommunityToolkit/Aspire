// CommunityToolkit.Aspire.Hosting.Squad — example consumer ApiApp
//
// Receives TWO `squad://...` connection strings from the AppHost (research-squad
// and dev-squad, both via WithReference) and registers a SquadAgent for each
// under a keyed DI service. The /ask and /dispatch endpoints take a
// ?squad=research|dev query parameter to pick which team handles the request,
// so you can hit the same API with two completely different multi-agent
// personalities and see them as separate traces in the Aspire dashboard.
//
// Two layers of observability are wired up here:
//
//  1. OpenTelemetry tracing — two activity sources show up in the Aspire
//     dashboard Traces view:
//
//       • Microsoft.Agents.AI.Squad   (emitted by Squad.Agents.AI)
//         One "squad.subagent {Name}" span per subagent dispatch, tagged with
//         squad.subagent.name / display_name / sdk_agent_id / reply_preview.
//
//       • Squad.Hosting.ApiApp        (emitted by THIS app)
//         One "squad.dispatch {endpoint} {squad}" span wraps each request so
//         the per-request trace tree has a clear parent and every subagent
//         lifecycle event (start / message / completed) lands on it as an
//         OpenTelemetry ActivityEvent annotation visible on the span timeline.
//
//  2. ILogger structured logs — each subagent start / message / done is
//     emitted via ILogger<Program>, so it shows up in the Aspire dashboard's
//     Structured Logs view with the squad name, subagent name, and message
//     preview as structured fields (queryable via the Filter bar).
//
// Why both? The SDK's spans may end up as orphan traces because the SDK's
// event pump (OnEvent) can fire on a background thread where Activity.Current
// has been cleared by AsyncLocal flow. The "squad.dispatch ..." wrapper span
// guarantees a single root per request that the user's trace search lands on.

using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Squad.Agents.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Surface both activity sources in the Aspire dashboard:
//  - Microsoft.Agents.AI.Squad → per-subagent spans (emitted by Squad.Agents.AI)
//  - Squad.Hosting.ApiApp      → per-request wrapper span (emitted below)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(SquadAgentDiagnostics.ActivitySourceName)
        .AddSource(ApiAppDiagnostics.ActivitySourceName));

// Resolve both squads from the WithReference-supplied connection strings (Aspire
// injects them under ConnectionStrings:{resourceName}). Each becomes a keyed
// SquadAgent so the /ask and /dispatch endpoints can pick by ?squad= at request
// time. The Aspire resource name is "{name}-squad" (e.g. "research-squad") and
// the query-param-friendly short name ("research") is the keyed-DI key.
var squadTeamRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var (key, resource, instructions) in new[]
{
    ("research", "research-squad", "You are the coordinator of an AI/ML research squad. Be concise."),
    ("dev",      "dev-squad",      "You are the coordinator of a full-stack development squad. Be concise."),
})
{
    var cs = builder.Configuration.GetConnectionString(resource)
        ?? throw new InvalidOperationException(
            $"Missing connection string for '{resource}'. " +
            $"Confirm the AppHost wires the Squad resource via .WithReference({resource}).");

    var root = ParseSquadTeamRoot(cs)
        ?? throw new InvalidOperationException(
            $"Could not parse teamRoot from connection string '{cs}'.");

    squadTeamRoots[key] = root;

    // Step 1: register the keyed SquadAgent with its squad-specific config.
    builder.Services.AddKeyedSquadAgent(key, opts =>
    {
        opts.SquadFolderPath = root;
        opts.AgentName = resource;
        opts.Instructions = instructions;
    });

    // Step 2: wire OnSubagentTrace via Configure<ILoggerFactory> so the
    // callback can use a real ILogger (vs Console.WriteLine, which does
    // not always surface in the Aspire dashboard). Options post-configure
    // runs after the host is built, so ILoggerFactory is fully constructed
    // by the time SquadAgent first calls .Get(optionsName).
    var capturedKey = key;
    var capturedResource = resource;
    builder.Services.AddOptions<SquadAgentOptions>(key)
        .Configure<ILoggerFactory>((opts, loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger($"Squad.Subagent.{capturedResource}");

            opts.OnSubagentTrace = trace =>
            {
                // Snapshot Activity.Current — this is the SDK's per-subagent span
                // when it is open (callback fires from inside the SDK's event pump
                // right after StartActivity), or whatever ambient span exists.
                var current = Activity.Current;

                switch (trace.Kind)
                {
                    case SquadAgentTraceEventKind.SubagentStarted:
                        logger.LogInformation(
                            "[{Squad}] >> subagent start: {SubagentName} (sdkId={SdkAgentId})",
                            capturedKey, trace.SubagentName, trace.SdkAgentId);
                        current?.AddEvent(new ActivityEvent(
                            "squad.subagent.start",
                            tags: new ActivityTagsCollection
                            {
                                ["squad.name"] = capturedKey,
                                ["squad.subagent.name"] = trace.SubagentName,
                                ["squad.subagent.sdk_agent_id"] = trace.SdkAgentId,
                            }));
                        break;

                    case SquadAgentTraceEventKind.SubagentCompleted:
                        logger.LogInformation(
                            "[{Squad}] << subagent done:  {SubagentName} (sdkId={SdkAgentId})",
                            capturedKey, trace.SubagentName, trace.SdkAgentId);
                        current?.AddEvent(new ActivityEvent(
                            "squad.subagent.completed",
                            tags: new ActivityTagsCollection
                            {
                                ["squad.name"] = capturedKey,
                                ["squad.subagent.name"] = trace.SubagentName,
                                ["squad.subagent.sdk_agent_id"] = trace.SdkAgentId,
                            }));
                        break;

                    case SquadAgentTraceEventKind.AssistantMessage when !string.IsNullOrEmpty(trace.SdkAgentId):
                        var preview = (trace.Content ?? "").Replace("\n", " ");
                        if (preview.Length > 200) preview = preview.Substring(0, 200) + "...";
                        logger.LogInformation(
                            "[{Squad}]    msg from {SubagentName}: {Preview}",
                            capturedKey, trace.SubagentName ?? trace.SdkAgentId, preview);
                        current?.AddEvent(new ActivityEvent(
                            "squad.subagent.message",
                            tags: new ActivityTagsCollection
                            {
                                ["squad.name"] = capturedKey,
                                ["squad.subagent.name"] = trace.SubagentName ?? trace.SdkAgentId,
                                ["squad.subagent.message_preview"] = preview,
                            }));
                        break;
                }
            };
        });
}

var app = builder.Build();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new
{
    squads = squadTeamRoots,
    endpoints = new[]
    {
        "/ask?squad=research      POST  — 3-turn conversation with one squad (session memory demo)",
        "/dispatch?squad=research POST  — forces multi-subagent dispatch (subagent observability demo)",
        "/swagger                 GET   — interactive UI",
    },
    hint = "Pick squad=research (Matrix cast) or squad=dev (Simpsons cast). Open the Aspire dashboard Traces view to see the squad.dispatch + squad.subagent spans, and Structured Logs view for per-subagent log lines.",
}));

// 3-turn smoke conversation against the picked Squad team.
// Demonstrates: AgentSession multi-turn memory + real agent invocation.
app.MapPost("/ask",
    async ([AsParameters] SquadQuery q, AskRequest req, IServiceProvider sp,
           ILogger<Program> logger, CancellationToken ct) =>
    {
        var agent = ResolveSquad(sp, q.Squad, out var error);
        if (agent is null) return Results.BadRequest(new { error });

        using var activity = ApiAppDiagnostics.ActivitySource
            .StartActivity($"squad.dispatch ask {q.Squad}", ActivityKind.Server);
        activity?.SetTag("squad.name", q.Squad);
        activity?.SetTag("squad.endpoint", "ask");
        activity?.SetTag("squad.prompt.count", req.Prompts.Length);

        logger.LogInformation("/ask squad={Squad} prompts={Count}", q.Squad, req.Prompts.Length);

        var turns = new List<TurnResult>();
        var session = await agent.CreateSessionAsync(ct);

        foreach (var prompt in req.Prompts)
        {
            var response = await agent.RunAsync(prompt, session, cancellationToken: ct);
            turns.Add(new TurnResult(prompt, response.Text));
        }

        return Results.Ok(new { squad = q.Squad, teamRoot = squadTeamRoots[q.Squad], turns });
    })
    .WithName("Ask")
    .WithOpenApi();

// Forces real subagent dispatch via the coordinator's task tool. Each subagent
// spawn shows up as its own "squad.subagent {Name}" span in the Aspire dashboard
// trace view, nested under (or alongside) the "squad.dispatch dispatch ..."
// wrapper span emitted below.
app.MapPost("/dispatch",
    async ([AsParameters] SquadQuery q, IServiceProvider sp,
           ILogger<Program> logger, CancellationToken ct) =>
    {
        var agent = ResolveSquad(sp, q.Squad, out var error);
        if (agent is null) return Results.BadRequest(new { error });

        using var activity = ApiAppDiagnostics.ActivitySource
            .StartActivity($"squad.dispatch dispatch {q.Squad}", ActivityKind.Server);
        activity?.SetTag("squad.name", q.Squad);
        activity?.SetTag("squad.endpoint", "dispatch");

        logger.LogInformation("/dispatch squad={Squad}", q.Squad);

        var session = await agent.CreateSessionAsync(ct);

        // The prompt is squad-specific so we get realistic dispatches into the
        // characters we actually have on each roster. Both variants use the same
        // "use the task tool to dispatch ..." phrasing that reliably triggers
        // real subagent spawning (a casual "team, ..." phrasing tends to make
        // the coordinator role-play subagents in a single response instead).
        var prompt = string.Equals(q.Squad, "research", StringComparison.OrdinalIgnoreCase)
            ? "Use the task tool to dispatch two parallel subagents. " +
              "Send the Research Lead (Morpheus) this exact prompt: " +
              "\"In one sentence, what is the most important property of a good research hypothesis?\" " +
              "Send the ML / Data Researcher (Trinity) this exact prompt: " +
              "\"In one sentence, what is the most important property of a reliable training data pipeline?\" " +
              "Wait for both to return, then output ONLY the two answers verbatim " +
              "(each on its own line, prefixed with the agent name), and nothing else."
            : "Use the task tool to dispatch two parallel subagents. " +
              "Send the Tech Lead (Lisa) this exact prompt: " +
              "\"In one sentence, what is the most important property of a good software architecture?\" " +
              "Send the Backend Developer (Frink) this exact prompt: " +
              "\"In one sentence, what is the most important property of a well-designed API?\" " +
              "Wait for both to return, then output ONLY the two answers verbatim " +
              "(each on its own line, prefixed with the agent name), and nothing else.";

        var response = await agent.RunAsync(prompt, session, cancellationToken: ct);

        activity?.SetTag("squad.response.length", response.Text?.Length ?? 0);

        return Results.Ok(new
        {
            squad = q.Squad,
            teamRoot = squadTeamRoots[q.Squad],
            prompt,
            response = response.Text,
            hint = "Open the Aspire dashboard's Traces view to see the squad.dispatch + squad.subagent spans, and Structured Logs for the per-subagent log lines.",
        });
    })
    .WithName("Dispatch")
    .WithOpenApi();

app.Run();

// ─── Helpers ──────────────────────────────────────────────────────────────────

SquadAgent? ResolveSquad(IServiceProvider sp, string key, out string? error)
{
    if (!squadTeamRoots.ContainsKey(key))
    {
        error = $"Unknown squad '{key}'. Use squad=research or squad=dev.";
        return null;
    }
    error = null;
    return sp.GetRequiredKeyedService<SquadAgent>(key);
}

static string? ParseSquadTeamRoot(string connectionString)
{
    if (!Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        return null;

    var query = uri.Query.TrimStart('?');
    foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
    {
        var parts = pair.Split('=', 2);
        if (parts.Length == 2 && parts[0].Equals("teamRoot", StringComparison.OrdinalIgnoreCase))
            return Uri.UnescapeDataString(parts[1]);
    }
    return null;
}

internal sealed record SquadQuery(string Squad = "research");

internal sealed record AskRequest(string[] Prompts)
{
    public static AskRequest Default { get; } = new(new[]
    {
        "Read .squad/team.md and list every member with their role. Just the names and roles, one per line.",
        "Pick one team member and summarize their charter in two sentences.",
        "Summarize this conversation in one sentence.",
    });
}

internal sealed record TurnResult(string Prompt, string? Response);

// ActivitySource owned by this app. Surfaced in the Aspire dashboard via
// AddSource(ApiAppDiagnostics.ActivitySourceName) wired up at startup.
internal static class ApiAppDiagnostics
{
    public const string ActivitySourceName = "Squad.Hosting.ApiApp";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
