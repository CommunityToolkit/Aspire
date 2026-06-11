// CommunityToolkit.Aspire.Hosting.Squad — example consumer ApiApp
//
// Receives TWO `squad://...` connection strings from the AppHost (research-squad and
// dev-squad, both via WithReference) and registers a SquadAgent for each under a
// keyed DI service. The /ask and /dispatch endpoints take a ?squad=research|dev
// query parameter to pick which team handles the request, so you can hit the same
// API with two completely different multi-agent personalities and see them as
// separate traces in the Aspire dashboard.
//
// Squad's subagent OpenTelemetry spans (Microsoft.Agents.AI.Squad) are wired into
// the OTel tracer here. Hitting /dispatch produces a multi-span trace per request:
// the HTTP POST root span, the SDK's task-tool span, and one "squad.subagent {Name}"
// span per spawned specialist (tagged with squad.subagent.name + reply_preview).

using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Squad.Agents.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Surface Squad's per-subagent OpenTelemetry spans in the Aspire dashboard.
// One span per subagent dispatch ("squad.subagent {Name}") with
// squad.subagent.name / display_name / sdk_agent_id / reply_preview tags.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(SquadAgentDiagnostics.ActivitySourceName));

// Resolve both squads from the WithReference-supplied connection strings (Aspire
// injects them under ConnectionStrings:{resourceName}). Each becomes a keyed
// SquadAgent so the /ask and /dispatch endpoints can pick by ?squad= at request time.
const string ResearchSquad = "research-squad";
const string DevSquad      = "dev-squad";
var squadTeamRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var resource in new[] { ResearchSquad, DevSquad })
{
    var cs = builder.Configuration.GetConnectionString(resource)
        ?? throw new InvalidOperationException(
            $"Missing connection string for '{resource}'. " +
            $"Confirm the AppHost wires the Squad resource via .WithReference({resource}).");

    var root = ParseSquadTeamRoot(cs)
        ?? throw new InvalidOperationException(
            $"Could not parse teamRoot from connection string '{cs}'.");

    squadTeamRoots[resource] = root;

    builder.Services.AddKeyedSquadAgent(resource, opts =>
    {
        opts.SquadFolderPath = root;
        opts.AgentName = resource;
        opts.Instructions = resource == ResearchSquad
            ? "You are the coordinator of an AI/ML research squad. Be concise."
            : "You are the coordinator of a full-stack development squad. Be concise.";

        // Forward subagent dispatch events to the Aspire dashboard via Console
        // (which the .NET hosting integration captures as structured logs) so each
        // spawn/reply/done is correlated with the matching OTel span by trace id.
        opts.OnSubagentTrace = trace =>
        {
            switch (trace.Kind)
            {
                case SquadAgentTraceEventKind.SubagentStarted:
                    Console.WriteLine($"[{resource}] >> subagent start: {trace.SubagentName} (sdkId={trace.SdkAgentId})");
                    break;
                case SquadAgentTraceEventKind.SubagentCompleted:
                    Console.WriteLine($"[{resource}] << subagent done:  {trace.SubagentName} (sdkId={trace.SdkAgentId})");
                    break;
                case SquadAgentTraceEventKind.AssistantMessage when !string.IsNullOrEmpty(trace.SdkAgentId):
                    var preview = (trace.Content ?? "").Replace("\n", " ");
                    if (preview.Length > 200) preview = preview.Substring(0, 200) + "...";
                    Console.WriteLine($"[{resource}]    msg from {trace.SubagentName ?? trace.SdkAgentId}: {preview}");
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
    hint = "Pick squad=research (Matrix cast) or squad=dev (Simpsons cast). Open the Aspire dashboard Traces view to see the squad.subagent spans.",
}));

// 3-turn smoke conversation against the picked Squad team.
// Demonstrates: AgentSession multi-turn memory + real agent invocation.
app.MapPost("/ask",
    async ([AsParameters] SquadQuery q, AskRequest req, IServiceProvider sp, CancellationToken ct) =>
    {
        var agent = ResolveSquad(sp, q.Squad, out var error);
        if (agent is null) return Results.BadRequest(new { error });

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
// trace view. This is the headline visibility demo.
app.MapPost("/dispatch",
    async ([AsParameters] SquadQuery q, IServiceProvider sp, CancellationToken ct) =>
    {
        var agent = ResolveSquad(sp, q.Squad, out var error);
        if (agent is null) return Results.BadRequest(new { error });

        var session = await agent.CreateSessionAsync(ct);

        // The prompt is squad-specific so we get realistic dispatches into the
        // characters we actually have on each roster. Both variants use the same
        // "use the task tool to dispatch ..." phrasing that reliably triggers
        // real subagent spawning (a casual "team, ..." phrasing tends to make
        // the coordinator role-play subagents in a single response instead).
        var prompt = string.Equals(q.Squad, ResearchSquad, StringComparison.OrdinalIgnoreCase)
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
        return Results.Ok(new
        {
            squad = q.Squad,
            teamRoot = squadTeamRoots[q.Squad],
            prompt,
            response = response.Text,
            hint = "Open the Aspire dashboard's Traces view to see the squad.subagent spans for each spawned specialist.",
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
