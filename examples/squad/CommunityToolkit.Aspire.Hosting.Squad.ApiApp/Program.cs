// CommunityToolkit.Aspire.Hosting.Squad — example consumer ApiApp
//
// Receives TWO `squad://...` connection strings from the AppHost (research-squad
// and dev-squad, both via WithReference). Each becomes a keyed SquadAgent via
// AddKeyedSquadAgent("{resource-name}") — Squad.Agents.AI 0.4.0+ looks up the
// Aspire-injected connection string under ConnectionStrings:{resource-name}
// directly, and Squad.Agents.AI 0.5.1+ also auto-injects `--agent squad` so the
// wrapped Copilot CLI loads .github/agents/squad.agent.md as the coordinator
// system prompt (matching `copilot --agent squad` in a terminal). No GetConnectionString
// or CliArgs boilerplate required.
//
// The single POST /ask endpoint takes a ?squad=research|dev query parameter and a
// JSON body with a list of prompts. The coordinator decides per-prompt whether to
// answer directly (Direct Mode in squad.agent.md), do a single agent spawn
// (Lightweight), or fan out via the task tool (Full). The "Sample prompts"
// section returned from GET / shows what each mode looks like — paste them
// straight into /ask to drive the observability surfaces.
//
// Two layers of observability are wired up here:
//
//  1. OpenTelemetry tracing — two activity sources show up in the Aspire
//     dashboard Traces view:
//
//       • Microsoft.Agents.AI.Squad   (emitted by Squad.Agents.AI 0.4.0+)
//         One "squad.subagent {Name}" span per subagent dispatch, with
//         "squad.subagent.start", "squad.subagent.message",
//         "squad.subagent.completed", "squad.subagent.failed" ActivityEvents
//         annotated on the span timeline. NO consumer wiring required — just
//         AddSource(SquadAgentDiagnostics.ActivitySourceName) on the tracer.
//
//       • Squad.Hosting.ApiApp        (emitted by THIS app)
//         One "squad.ask {squad}" span wraps each /ask request so the trace
//         tree has a clear root the user's trace search can land on.
//
//  2. ILogger structured logs — per-squad subagent start / message / done is
//     emitted via a typed OnSubagentTrace callback that delegates to ILogger.
//     Visible in the Aspire dashboard's Structured Logs view with the squad
//     name, subagent name, and message preview as queryable structured fields.

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
//  - Microsoft.Agents.AI.Squad → per-subagent spans (default-on as of 0.4.0)
//  - Squad.Hosting.ApiApp      → per-request wrapper span (emitted below)
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(SquadAgentDiagnostics.ActivitySourceName)
        .AddSource(ApiAppDiagnostics.ActivitySourceName));

// Register one keyed SquadAgent per Aspire-injected squad resource. The
// resource name IS the keyed-DI key AND the IConfiguration connection-string
// key (Squad.Agents.AI 0.4.0+ picks up ConnectionStrings:{name} directly).
// Squad.Agents.AI 0.5.1+ also auto-injects `--agent squad` when
// .github/agents/squad.agent.md exists, so the wrapped session behaves the
// same as running `copilot --agent squad` from a terminal — eager execution,
// parallel fan-out, real subagent dispatch through the task tool.
//
// Net result: a Squad coordinator team with full Aspire wiring (connection
// string + OTel spans + structured logs) in one line per squad.
foreach (var key in new[] { "research-squad", "dev-squad" })
{
    builder.Services.AddKeyedSquadAgent(key, opts =>
    {
        opts.AgentName = key;
    });

    // Optional: forward typed trace events to ILogger as structured logs. Telemetry
    // (the OTel spans + ActivityEvents) is independent of this callback — it is
    // already on by default and renders in the Aspire dashboard's Traces view.
    // We only set this callback to also surface a per-subagent line in the
    // Structured Logs view (queryable by squad name + subagent name).
    var capturedKey = key;
    builder.Services.AddOptions<SquadAgentOptions>(key)
        .Configure<ILoggerFactory>((opts, loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger($"Squad.Subagent.{capturedKey}");
            opts.OnSubagentTrace = trace =>
            {
                switch (trace.Kind)
                {
                    case SquadAgentTraceEventKind.SubagentStarted:
                        logger.LogInformation(
                            "[{Squad}] >> subagent start: {SubagentName} (sdkId={SdkAgentId})",
                            capturedKey, trace.SubagentName, trace.SdkAgentId);
                        break;
                    case SquadAgentTraceEventKind.SubagentCompleted:
                        logger.LogInformation(
                            "[{Squad}] << subagent done:  {SubagentName} (sdkId={SdkAgentId})",
                            capturedKey, trace.SubagentName, trace.SdkAgentId);
                        break;
                    case SquadAgentTraceEventKind.AssistantMessage when !string.IsNullOrEmpty(trace.SdkAgentId):
                        var preview = (trace.Content ?? "").Replace("\n", " ");
                        if (preview.Length > 200) preview = preview.Substring(0, 200) + "...";
                        logger.LogInformation(
                            "[{Squad}]    msg from {SubagentName}: {Preview}",
                            capturedKey, trace.SubagentName ?? trace.SdkAgentId, preview);
                        break;
                }
            };
        });
}

// Map the user-facing short name (?squad=research) to the Aspire resource name
// (research-squad) used as the keyed-DI key. Keeps the query string ergonomic
// without renaming the Aspire resources.
var squadKeysByShortName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["research"] = "research-squad",
    ["dev"]      = "dev-squad",
};

var app = builder.Build();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Index: shows the single /ask endpoint plus a copy-paste menu of sample
// prompts that exercise each coordinator mode (Direct, Lightweight, Full).
// Same body shape as /ask accepts — just `{"prompts": [...]}`.
app.MapGet("/", () => Results.Ok(new
{
    squads = squadKeysByShortName,
    endpoint = "POST /ask?squad=research|dev — body: { \"prompts\": [...] }. " +
               "Each prompt runs sequentially on a single AgentSession (multi-turn memory). " +
               "The coordinator picks Direct / Lightweight / Full mode per prompt; only Full mode produces squad.subagent spans.",
    sample_prompts = new
    {
        roster_recall_direct_mode = new
        {
            squad = "dev",
            prompts = new[] { "Who's on the team? Just names and roles." },
            note = "Direct Mode — coordinator answers from team.md. No squad.subagent spans.",
        },
        multi_turn_memory = new
        {
            squad = "research",
            prompts = new[]
            {
                "Read .squad/team.md and list every member with their role. Just the names and roles, one per line.",
                "Pick one team member and summarize their charter in two sentences.",
                "Summarize this conversation in one sentence.",
            },
            note = "Same AgentSession across all three turns — the third prompt references context from the first two.",
        },
        dispatch_full_mode_research = new
        {
            squad = "research",
            prompts = new[]
            {
                "Use the task tool to dispatch two parallel subagents. " +
                "Send the Research Lead (Morpheus) this exact prompt: " +
                "\"In one sentence, what is the most important property of a good research hypothesis?\" " +
                "Send the ML / Data Researcher (Trinity) this exact prompt: " +
                "\"In one sentence, what is the most important property of a reliable training data pipeline?\" " +
                "Wait for both to return, then output ONLY the two answers verbatim " +
                "(each on its own line, prefixed with the agent name), and nothing else.",
            },
            note = "Full Mode — produces 2 'squad.subagent {Morpheus|Trinity}' spans with start/message/completed ActivityEvents. Headline observability demo.",
        },
        dispatch_full_mode_dev = new
        {
            squad = "dev",
            prompts = new[]
            {
                "Use the task tool to dispatch two parallel subagents. " +
                "Send the Tech Lead (Lisa) this exact prompt: " +
                "\"In one sentence, what is the most important property of a good software architecture?\" " +
                "Send the Backend Developer (Frink) this exact prompt: " +
                "\"In one sentence, what is the most important property of a well-designed API?\" " +
                "Wait for both to return, then output ONLY the two answers verbatim " +
                "(each on its own line, prefixed with the agent name), and nothing else.",
            },
            note = "Full Mode — produces 2 'squad.subagent {Lisa|Frink}' spans. Same as research but using the dev-squad cast.",
        },
    },
    hint = "Open the Aspire dashboard's Traces view to see squad.ask + squad.subagent spans, and Structured Logs (filter category=Squad.Subagent.*) for per-subagent log lines.",
}));

// Single endpoint: send any prompt(s) you like. The coordinator picks the
// right mode (Direct / Lightweight / Full) based on what you ask for.
// Sample prompts that drive each mode are listed at GET /.
app.MapPost("/ask",
    async ([AsParameters] SquadQuery q, AskRequest req, IServiceProvider sp,
           ILogger<Program> logger, CancellationToken ct) =>
    {
        var agent = ResolveSquad(sp, q.Squad, out var error);
        if (agent is null) return Results.BadRequest(new { error });

        using var activity = ApiAppDiagnostics.ActivitySource
            .StartActivity($"squad.ask {q.Squad}", ActivityKind.Server);
        activity?.SetTag("squad.name", q.Squad);
        activity?.SetTag("squad.prompt.count", req.Prompts.Length);

        logger.LogInformation("/ask squad={Squad} prompts={Count}", q.Squad, req.Prompts.Length);

        var turns = new List<TurnResult>();
        var session = await agent.CreateSessionAsync(ct);

        foreach (var prompt in req.Prompts)
        {
            var response = await agent.RunAsync(prompt, session, cancellationToken: ct);
            turns.Add(new TurnResult(prompt, response.Text));
        }

        return Results.Ok(new { squad = q.Squad, turns });
    })
    .WithName("Ask")
    .WithOpenApi();

app.Run();

// ─── Helpers ──────────────────────────────────────────────────────────────────

SquadAgent? ResolveSquad(IServiceProvider sp, string shortName, out string? error)
{
    if (!squadKeysByShortName.TryGetValue(shortName, out var resourceName))
    {
        error = $"Unknown squad '{shortName}'. Use squad=research or squad=dev.";
        return null;
    }
    error = null;
    return sp.GetRequiredKeyedService<SquadAgent>(resourceName);
}

internal sealed record SquadQuery(string Squad = "research");

internal sealed record AskRequest(string[] Prompts);

internal sealed record TurnResult(string Prompt, string? Response);

// ActivitySource owned by this app. Surfaced in the Aspire dashboard via
// AddSource(ApiAppDiagnostics.ActivitySourceName) wired up at startup.
internal static class ApiAppDiagnostics
{
    public const string ActivitySourceName = "Squad.Hosting.ApiApp";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
