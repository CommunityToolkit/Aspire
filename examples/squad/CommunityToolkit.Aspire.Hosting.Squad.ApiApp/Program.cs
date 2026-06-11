// CommunityToolkit.Aspire.Hosting.Squad — example consumer ApiApp
//
// Receives a `squad://...` connection string from the AppHost (via WithReference)
// and uses Squad.Agents.AI to construct a SquadAgent that drives the referenced
// Squad team. Exposes /ask (3-turn conversation) and /dispatch (forces subagent
// dispatch so you can see Squad's OpenTelemetry spans light up in the Aspire
// dashboard trace view).
//
// The connection string is supplied by Aspire as ConnectionStrings:research-squad.
// Format:
//   squad://resource/{name}?teamRoot={escaped path}&agents={csv}&protocol=maf-1.0

using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Squad.Agents.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Squad.Agents.AI's ActivitySource to the OpenTelemetry tracer wired up by
// AddServiceDefaults. One span per subagent dispatch ("squad.subagent {Name}")
// flows to the Aspire dashboard's trace view, tagged with squad.subagent.name,
// squad.subagent.display_name, squad.subagent.sdk_agent_id, and
// squad.subagent.reply_preview.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(SquadAgentDiagnostics.ActivitySourceName));

// Resolve the Squad team root from the WithReference-supplied connection string.
// Aspire injects connection strings under ConnectionStrings:{resourceName}.
const string SquadResourceName = "research-squad";
var connectionString = builder.Configuration.GetConnectionString(SquadResourceName)
    ?? throw new InvalidOperationException(
        $"Missing connection string for '{SquadResourceName}'. " +
        "Confirm the AppHost wires the Squad resource via .WithReference(squad).");

var teamRoot = ParseSquadTeamRoot(connectionString)
    ?? throw new InvalidOperationException(
        $"Could not parse teamRoot from connection string '{connectionString}'.");

// Register a SquadAgent that points at the team supplied by Aspire. ConfigureSession
// turns on subagent streaming so each subagent's reply propagates to the parent
// session; OnSubagentTrace logs subagent dispatch lifecycle so it also surfaces in
// the Aspire dashboard's structured-log view (in addition to the OTel spans).
builder.Services.AddSquadAgent(opts =>
{
    opts.SquadFolderPath = teamRoot;
    opts.AgentName = "research-squad";
    opts.Instructions = "You are a research assistant. Be concise.";

    opts.OnSubagentTrace = trace =>
    {
        // Lazy resolve a logger via Activity.Current — the AspNetCore request scope
        // is active here and the OTel tracer will correlate the structured log with
        // the dispatch span automatically.
        var logger = (ILogger?)null;
        switch (trace.Kind)
        {
            case SquadAgentTraceEventKind.SubagentStarted:
                Console.WriteLine($"[squad] >> subagent start: {trace.SubagentName} (sdkId={trace.SdkAgentId})");
                break;
            case SquadAgentTraceEventKind.SubagentCompleted:
                Console.WriteLine($"[squad] << subagent done:  {trace.SubagentName} (sdkId={trace.SdkAgentId})");
                break;
            case SquadAgentTraceEventKind.AssistantMessage when !string.IsNullOrEmpty(trace.SdkAgentId):
                var preview = (trace.Content ?? "").Replace("\n", " ");
                if (preview.Length > 200) preview = preview.Substring(0, 200) + "...";
                Console.WriteLine($"[squad]    msg from {trace.SubagentName ?? trace.SdkAgentId}: {preview}");
                break;
        }
        _ = logger;
    };
});

var app = builder.Build();
app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new
{
    teamRoot,
    resource = SquadResourceName,
    endpoints = new[] { "/ask", "/dispatch", "/health", "/swagger" },
    note = "POST /dispatch to see real subagent dispatch + Squad OTel spans in the Aspire dashboard trace view."
}));

// 3-turn smoke conversation against the referenced Squad team.
// Demonstrates: AgentSession multi-turn memory + real agent invocation.
app.MapPost("/ask", async (AskRequest req, SquadAgent agent, CancellationToken ct) =>
{
    var turns = new List<TurnResult>();
    AgentSession session = await agent.CreateSessionAsync(ct);

    foreach (var prompt in req.Prompts)
    {
        var response = await agent.RunAsync(prompt, session, cancellationToken: ct);
        turns.Add(new TurnResult(prompt, response.Text));
    }

    return Results.Ok(new { teamRoot, turns });
})
.WithName("Ask")
.WithOpenApi();

// Forces real subagent dispatch via the coordinator's task tool. Each subagent
// spawn shows up as its own "squad.subagent {Name}" span in the Aspire dashboard
// trace view, with a preview of the subagent's reply tagged on the span when it
// closes. This is the headline visibility demo.
app.MapPost("/dispatch", async (SquadAgent agent, CancellationToken ct) =>
{
    AgentSession session = await agent.CreateSessionAsync(ct);
    const string Prompt =
        "Use the task tool to dispatch two parallel subagents. " +
        "Send the first subagent (any team architect/lead) this exact prompt: " +
        "\"In one sentence, what is the most important property of an agent framework architecture?\" " +
        "Send the second subagent (any team code expert / engineer) this exact prompt: " +
        "\"In one sentence, what is the most important property of an agent framework implementation?\" " +
        "Wait for both to return, then output ONLY the two answers verbatim " +
        "(each on its own line, prefixed with the agent name), and nothing else.";

    var response = await agent.RunAsync(Prompt, session, cancellationToken: ct);
    return Results.Ok(new
    {
        teamRoot,
        prompt = Prompt,
        response = response.Text,
        hint = "Open the Aspire dashboard's Traces view to see the two squad.subagent spans."
    });
})
.WithName("Dispatch")
.WithOpenApi();

app.Run();

// ─── Helpers ──────────────────────────────────────────────────────────────────

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

internal sealed record AskRequest(string[] Prompts)
{
    public static AskRequest Default { get; } = new(new[]
    {
        "My name is Alice and I love hiking.",
        "What do you remember about me?",
        "Summarize this conversation in one sentence."
    });
}

internal sealed record TurnResult(string Prompt, string? Response);
