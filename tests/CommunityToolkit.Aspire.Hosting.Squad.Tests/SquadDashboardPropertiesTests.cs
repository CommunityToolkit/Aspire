using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

/// <summary>
/// Covers <c>SquadDashboardProperties</c> — the property snapshot the lifecycle hook publishes
/// to the Aspire dashboard. These tests run synchronously against the disk so the live-stats path
/// (<c>CreateWithLiveStats</c>) is exercised end-to-end without mocking <c>File</c>/<c>Directory</c>.
/// </summary>
public class SquadDashboardPropertiesTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

    [Fact]
    public void CreateStatic_ContainsAllKnownProperties()
    {
        var teamRoot = CreateTeamRoot();
        var resource = new SquadResource("squad", teamRoot);

        var properties = SquadDashboardProperties.CreateStatic(resource);

        var keys = properties.Select(p => p.Name).ToHashSet();
        Assert.Contains("Squad location", keys);
        Assert.Contains("Team roster", keys);
        Assert.Contains("Decisions", keys);
        Assert.Contains("Decision inbox", keys);
        Assert.Contains("Agent count", keys);
        Assert.Contains("Agents", keys);
        Assert.Contains("Protocol", keys);
        Assert.Contains("Runtime mode", keys);
        Assert.Contains("Capabilities", keys);

        Assert.Equal("maf-1.0", PropertyValue(properties, "Protocol"));
        Assert.Equal(teamRoot, PropertyValue(properties, "Squad location"));
    }

    [Fact]
    public void CreateStatic_NullResource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SquadDashboardProperties.CreateStatic(null!));
    }

    [Fact]
    public void CreateWithLiveStats_NoInboxDirectory_ReportsZeroPendingItems()
    {
        var teamRoot = CreateTeamRoot(); // .squad/ exists, .squad/decisions/inbox/ does not
        var resource = new SquadResource("squad", teamRoot);

        var properties = SquadDashboardProperties.CreateWithLiveStats(resource);

        Assert.Equal("0", PropertyValue(properties, "Pending inbox items"));
        Assert.Equal("none", PropertyValue(properties, "Last decision"));
    }

    [Fact]
    public void CreateWithLiveStats_WithMarkdownFilesInInbox_ReportsCount()
    {
        var teamRoot = CreateTeamRoot();
        var inbox = Path.Combine(teamRoot, ".squad", "decisions", "inbox");
        Directory.CreateDirectory(inbox);
        File.WriteAllText(Path.Combine(inbox, "one.md"), "# pending");
        File.WriteAllText(Path.Combine(inbox, "two.md"), "# pending");
        File.WriteAllText(Path.Combine(inbox, "ignore.txt"), "not counted");

        var resource = new SquadResource("squad", teamRoot);
        var properties = SquadDashboardProperties.CreateWithLiveStats(resource);

        Assert.Equal("2", PropertyValue(properties, "Pending inbox items"));
    }

    [Fact]
    public void CreateWithLiveStats_WithDecisionsMd_ReportsFirstLine()
    {
        var teamRoot = CreateTeamRoot();
        var decisionsMd = Path.Combine(teamRoot, ".squad", "decisions.md");

        // First non-blank line is "# Use Postgres" — verify leading '#' / spaces are stripped.
        File.WriteAllText(decisionsMd, "\n\n# Use Postgres for primary store\n\nMore detail here.");

        var resource = new SquadResource("squad", teamRoot);
        var properties = SquadDashboardProperties.CreateWithLiveStats(resource);

        Assert.Equal("Use Postgres for primary store", PropertyValue(properties, "Last decision"));
    }

    [Fact]
    public void CreateWithLiveStats_WithLongDecisionLine_TruncatesAt80Chars()
    {
        var teamRoot = CreateTeamRoot();
        var decisionsMd = Path.Combine(teamRoot, ".squad", "decisions.md");
        var longHeader = new string('x', 120);
        File.WriteAllText(decisionsMd, longHeader);

        var resource = new SquadResource("squad", teamRoot);
        var properties = SquadDashboardProperties.CreateWithLiveStats(resource);

        var lastDecision = PropertyValue(properties, "Last decision")!;
        Assert.EndsWith("...", lastDecision);
        Assert.Equal(80 + 3, lastDecision.Length); // 80 chars + the "..."
    }

    [Fact]
    public void CreateWithLiveStats_EmptyDecisionsMd_KeepsDefaultNone()
    {
        var teamRoot = CreateTeamRoot();
        var decisionsMd = Path.Combine(teamRoot, ".squad", "decisions.md");
        File.WriteAllText(decisionsMd, "\n\n\n"); // only whitespace lines

        var resource = new SquadResource("squad", teamRoot);
        var properties = SquadDashboardProperties.CreateWithLiveStats(resource);

        Assert.Equal("none", PropertyValue(properties, "Last decision"));
    }

    // ─────────────────────────────── helpers ───────────────────────────────

    private static string? PropertyValue(IEnumerable<ResourcePropertySnapshot> properties, string name) =>
        properties.SingleOrDefault(p => p.Name == name)?.Value?.ToString();

    private string CreateTeamRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ctk-aspire-squad-dashprops-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, ".squad"));
        _tempRoots.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempRoots)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
