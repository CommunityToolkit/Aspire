namespace Aspire.Hosting.ApplicationModel;

internal static class SquadDashboardProperties
{
    public const string Protocol = "maf-1.0";

    public static ResourcePropertySnapshot[] CreateStatic(SquadResource squad)
    {
        ArgumentNullException.ThrowIfNull(squad);

        var squadDirectory = Path.Combine(squad.TeamRoot, ".squad");
        var rosterFile = Path.Combine(squadDirectory, "team.md");
        var decisionsFile = Path.Combine(squadDirectory, "decisions.md");
        var inboxDirectory = Path.Combine(squadDirectory, "decisions", "inbox");

        return
        [
            new ResourcePropertySnapshot("Squad location", squad.TeamRoot),
            new ResourcePropertySnapshot("Team roster", rosterFile),
            new ResourcePropertySnapshot("Decisions", decisionsFile),
            new ResourcePropertySnapshot("Decision inbox", inboxDirectory),
            new ResourcePropertySnapshot("Agent count", squad.Agents.Count.ToString()),
            new ResourcePropertySnapshot("Agents", string.Join(", ", squad.Agents)),
            new ResourcePropertySnapshot("Protocol", Protocol),
            new ResourcePropertySnapshot("Runtime mode", "Aspire logical Squad resource"),
            new ResourcePropertySnapshot("Capabilities", "connection-string, dashboard-commands, live-maf-workflow"),
        ];
    }

    public static ResourcePropertySnapshot[] CreateWithLiveStats(SquadResource squad)
    {
        var staticProperties = CreateStatic(squad);
        var inboxDirectory = Path.Combine(squad.TeamRoot, ".squad", "decisions", "inbox");
        var decisionsFile = Path.Combine(squad.TeamRoot, ".squad", "decisions.md");

        var inboxDepth = Directory.Exists(inboxDirectory)
            ? Directory.GetFiles(inboxDirectory, "*.md").Length
            : 0;

        var lastDecision = "none";
        if (File.Exists(decisionsFile))
        {
            var firstLine = File.ReadLines(decisionsFile)
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

            if (firstLine is not null)
            {
                lastDecision = firstLine.TrimStart('#', ' ');
                if (lastDecision.Length > 80)
                {
                    lastDecision = lastDecision[..80] + "...";
                }
            }
        }

        return
        [
            ..staticProperties,
            new ResourcePropertySnapshot("Pending inbox items", inboxDepth.ToString()),
            new ResourcePropertySnapshot("Last decision", lastDecision),
        ];
    }
}
