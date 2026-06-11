// CommunityToolkit.Aspire.Hosting.Squad — example consumer ApiApp
//
// Receives TWO `squad://...` connection strings from the AppHost (research-squad
// and dev-squad, both via WithReference). Each becomes a keyed SquadAgent via
// AddKeyedSquadAgent("{resource-name}") — Squad.Agents.AI 0.4.0+ looks up the
// Aspire-injected connection string under ConnectionStrings:{resource-name}
// directly, so the example does not need any GetConnectionString / Uri-parse
// boilerplate. The /ask and /dispatch endpoints take a ?squad=research|dev
// query parameter to pick which team handles the request.
//
// Two layers of observability are wired up here:
//
//  1. OpenTelemetry tracing — two activity sources show up in the Aspire
//     dashboard Traces view:
//
//       • Microsoft.Agents.AI.Squad   (emitted by Squad.Agents.AI 0.4.0)
//         One "squad.subagent {Name}" span per subagent dispatch, with
//         "squad.subagent.start", "squad.subagent.message",
//         "squad.subagent.completed", "squad.subagent.failed" ActivityEvents
//         annotated on the span timeline. NO consumer wiring required — just
//         AddSource(SquadAgentDiagnostics.ActivitySourceName) on the tracer.
//
//       • Squad.Hosting.ApiApp        (emitted by THIS app)
//         One "squad.dispatch {endpoint} {squad}" span wraps each request so
//         the trace tree has a clear root the user's trace search can land on.
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
// key (Squad.Agents.AI 0.4.0 picks up ConnectionStrings:{name} directly).
// No need to fetch the connection string, parse the URI, or pull SquadFolderPath
// out by hand — the SDK does all of that.
//
// Two important configuration bits that make the coordinator behave the way it
// does when you run `copilot --agent squad` in a terminal:
//
//   --agent squad        Tells the Copilot CLI to load .github/agents/squad.agent.md
//                        as the agent definition. Without this the CLI uses its
//                        default agent and gets no squad-coordinator instructions,
//                        so it role-plays the team in a single reply instead of
//                        firing the `task` tool to spawn real subagents.
//
//   (no Instructions override) The terse "Be concise" instruction we used before
//                        was overriding the full 1k-line squad.agent.md system
//                        prompt (which is what teaches the coordinator to
//                        eager-execute, fan out, and dispatch through the task
//                        tool). Leaving Instructions unset lets the .agent.md
//                        file own the system prompt.
foreach (var key in new[] { "research-squad", "dev-squad" })
{
    builder.Services.AddKeyedSquadAgent(key, opts =>
    {
        opts.AgentName = key;
        opts.CliArgs.Add("--agent");
        opts.CliArgs.Add("squad");
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

// Reverse map ?squad=research|dev → Aspire resource name for keyed resolution.
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

app.MapGet("/", () => Results.Ok(new
{
    squads = squadKeysByShortName,
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

        return Results.Ok(new { squad = q.Squad, turns });
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
            prompt,
            response = response.Text,
            hint = "Open the Aspire dashboard's Traces view to see the squad.dispatch + squad.subagent spans, and Structured Logs for the per-subagent log lines.",
        });
    })
    .WithName("Dispatch")
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
