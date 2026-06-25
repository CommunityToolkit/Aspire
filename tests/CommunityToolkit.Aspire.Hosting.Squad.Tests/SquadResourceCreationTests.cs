using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

public class SquadResourceCreationTests : IDisposable
{
    // Track every temp dir created during a test so the disposer can delete them.
    // Prevents test residue from accumulating in %TEMP% across runs (especially in CI).
    private readonly List<string> _tempRoots = new();

    [Fact]
    public void AddSquad_RegistersSquadResource_WithGivenName()
    {
        var builder = DistributedApplication.CreateBuilder();
        var tempRoot = CreateEmptyTeamRoot();

        builder.AddSquad("research-squad", teamRoot: tempRoot);

        using var app = builder.Build();
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();

        var resource = appModel.Resources.OfType<SquadResource>().SingleOrDefault();
        Assert.NotNull(resource);
        Assert.Equal("research-squad", resource!.Name);
        Assert.Equal(tempRoot, resource.TeamRoot);
    }

    [Fact]
    public void AddSquad_WithNoTeamMd_ReturnsDefaultRoster()
    {
        var tempRoot = CreateEmptyTeamRoot();

        var resource = new SquadResource("squad", tempRoot);

        Assert.NotEmpty(resource.Agents);
        Assert.Contains("ralph", resource.Agents);
    }

    [Fact]
    public void AddSquad_WithMissingTeamRoot_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SquadResource("squad", string.Empty));
    }

    [Fact]
    public void AddSquad_WithNullName_ThrowsArgumentException()
    {
        var builder = DistributedApplication.CreateBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.AddSquad(null!, teamRoot: CreateEmptyTeamRoot()));
    }

    [Fact]
    public void AddSquad_TableFormatRoster_ParsesKnownAgents()
    {
        var tempRoot = CreateTeamRootWithRoster(
            teamMd: """
                ## Team

                | Ralph    | Work Monitor   |
                | Picard   | Lead           |
                | NotARealAgent | Unknown   |
                """,
            agentNames: ["ralph", "picard"]);

        var resource = new SquadResource("squad", tempRoot);

        Assert.Contains("ralph", resource.Agents);
        Assert.Contains("picard", resource.Agents);
        Assert.DoesNotContain("notarealagent", resource.Agents);
    }

    [Fact]
    public void AddSquad_BulletFormatRoster_ParsesKnownAgents()
    {
        var tempRoot = CreateTeamRootWithRoster(
            teamMd: """
                ## Team
                - **Ralph** (Work Monitor)
                - **Picard** (Lead)
                """,
            agentNames: ["ralph", "picard"]);

        var resource = new SquadResource("squad", tempRoot);

        Assert.Contains("ralph", resource.Agents);
        Assert.Contains("picard", resource.Agents);
    }

    [Fact]
    public void SquadResource_ConnectionString_HasExpectedShape()
    {
        var tempRoot = CreateTeamRootWithRoster(
            teamMd: "| Ralph | Work Monitor |",
            agentNames: ["ralph"]);

        var resource = new SquadResource("research-squad", tempRoot);
        var expression = resource.ConnectionStringExpression.Format;

        Assert.Contains("squad://resource/", expression);
        Assert.Contains("teamRoot=", expression);
        Assert.Contains("agents=", expression);
        Assert.Contains("protocol=maf-1.0", expression);
    }

    public void Dispose()
    {
        foreach (var dir in _tempRoots)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup. Never let teardown fail a test run.
            }
        }
    }

    // Helpers

    private string CreateEmptyTeamRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ctk-aspire-squad-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempRoots.Add(dir);
        return dir;
    }

    private string CreateTeamRootWithRoster(string teamMd, IEnumerable<string> agentNames)
    {
        var root = CreateEmptyTeamRoot();
        var squadDir = Path.Combine(root, ".squad");
        Directory.CreateDirectory(squadDir);
        File.WriteAllText(Path.Combine(squadDir, "team.md"), teamMd);
        var agentsDir = Path.Combine(squadDir, "agents");
        Directory.CreateDirectory(agentsDir);
        foreach (var name in agentNames)
        {
            var agentDir = Path.Combine(agentsDir, name);
            Directory.CreateDirectory(agentDir);
            File.WriteAllText(Path.Combine(agentDir, "charter.md"), $"# {name}");
        }
        return root;
    }
}
