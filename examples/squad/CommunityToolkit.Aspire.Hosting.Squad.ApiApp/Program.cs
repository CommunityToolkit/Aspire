// CommunityToolkit.Aspire.Hosting.Squad — example consumer ApiApp
//
// Receives a `squad://...` connection string from the AppHost (via WithReference)
// and uses Squad.Agents.AI to construct a SquadAgent that drives the referenced
// Squad team. Exposes a single /ask endpoint that runs a 3-turn conversation so
// you can see session memory + actual agent output in the dashboard.
//
// The connection string is supplied by Aspire as ConnectionStrings:research-squad.
// Format:
//   squad://resource/{name}?teamRoot={escaped path}&agents={csv}&protocol=maf-1.0

using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Squad.Agents.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// Register a SquadAgent that points at the team supplied by Aspire.
// The DI-friendly overload picks up SquadAgentOptions, an ILoggerFactory,
// and (when set) ConfigureSession from DI / the configure lambda.
builder.Services.AddSquadAgent(opts =>
{
    opts.SquadFolderPath = teamRoot;
    opts.AgentName = "research-squad";
    opts.Instructions = "You are a research assistant. Be concise.";
    // The default OnPermissionRequest is PermissionHandler.ApproveAll (shipped. Production hosts should override
    // this via opts.ConfigureSession to gate file/shell/MCP access.
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
    endpoints = new[] { "/ask", "/health", "/swagger" }
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
