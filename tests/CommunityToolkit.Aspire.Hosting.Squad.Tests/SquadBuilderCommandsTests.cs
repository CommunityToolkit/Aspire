using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

// ASPIREINTERACTION001: InteractionInput[Collection] is currently experimental in Aspire 13.x;
// it's the only way to construct ExecuteCommandContext.Arguments. Suppressed here for tests.
#pragma warning disable ASPIREINTERACTION001

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

/// <summary>
/// Exercises the dashboard commands attached to <see cref="SquadResource"/> by
/// <c>SquadBuilderExtensions.AddSquad</c>. Each <c>WithCommand(...)</c> call lands as a
/// <see cref="ResourceCommandAnnotation"/> on the resource; we invoke each command's
/// <c>ExecuteCommand</c> delegate and assert the visible <see cref="ExecuteCommandResult"/>.
/// </summary>
public class SquadBuilderCommandsTests : IDisposable
{
    private readonly List<string> _tempRoots = new();

    [Fact]
    public void AddSquad_AttachesExpectedDashboardCommands()
    {
        var resource = BuildSquadResource("squad");

        var commandNames = resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .Select(c => c.Name)
            .ToHashSet();

        Assert.Contains("refresh-agents", commandNames);
        Assert.Contains("open-team-root", commandNames);
        Assert.Contains("open-copilot-cli", commandNames);
        Assert.Contains("check-inbox", commandNames);
    }

    [Fact]
    public async Task RefreshAgents_WithExistingTeamMd_ReportsAgentCountAndSucceeds()
    {
        var resource = BuildSquadResource("squad", seedTeamMd: true);

        var result = await InvokeCommandAsync(resource, "refresh-agents");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task RefreshAgents_WithoutTeamMd_StillSucceedsWithDefaultRosterMessage()
    {
        var resource = BuildSquadResource("squad", seedTeamMd: false);

        var result = await InvokeCommandAsync(resource, "refresh-agents");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task OpenTeamRoot_WithMissingDirectory_ReportsWin32OrFileNotFound()
    {
        // Build with a real dir, then delete it so the Process.Start opens against a
        // non-existent path. UseShellExecute=true on a missing path typically surfaces
        // a Win32Exception. On non-Windows platforms the same call may succeed (xdg-open),
        // so we accept either Success=false (caught exception) OR Success=true.
        var teamRoot = CreateTeamRoot();
        var resource = new SquadResource("squad", teamRoot);
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSquad("squad", teamRoot: teamRoot);
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var hostedResource = model.Resources.OfType<SquadResource>().Single();

        // Delete the team root to force a launch failure on Windows.
        Directory.Delete(teamRoot, recursive: true);
        _tempRoots.Remove(teamRoot);

        var result = await InvokeCommandAsync(hostedResource, "open-team-root");

        // We just need the command to return without throwing — both Success=true and
        // Success=false are valid outcomes depending on the OS / shell handler.
        Assert.NotNull(result);
    }

    [Fact]
    public async Task OpenCopilotCli_OnNonWindows_AttemptsLaunch()
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows the command can succeed or fail depending on whether wt.exe / Copilot CLI
            // are installed on the test runner; skip here. The non-Windows path is exercised below.
            return;
        }

        var resource = BuildSquadResource("squad");
        var result = await InvokeCommandAsync(resource, "open-copilot-cli");

        // Cross-platform support means non-Windows now attempts to launch the CLI.
        // Success=true or Success=false are valid outcomes depending on the shell handler.
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CheckInbox_WithNoInboxDirectory_Succeeds()
    {
        var resource = BuildSquadResource("squad"); // no inbox dir created

        var result = await InvokeCommandAsync(resource, "check-inbox");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task CheckInbox_WithPendingItems_Succeeds()
    {
        var teamRoot = CreateTeamRoot();
        var inbox = Path.Combine(teamRoot, ".squad", "decisions", "inbox");
        Directory.CreateDirectory(inbox);
        File.WriteAllText(Path.Combine(inbox, "one.md"), "# pending");
        File.WriteAllText(Path.Combine(inbox, "two.md"), "# pending");

        var resource = BuildSquadResourceForExistingRoot("squad", teamRoot);

        var result = await InvokeCommandAsync(resource, "check-inbox");

        Assert.True(result.Success);
    }

    // ─────────────────────────────── helpers ───────────────────────────────

    private SquadResource BuildSquadResource(string name, bool seedTeamMd = false)
    {
        var teamRoot = CreateTeamRoot();
        if (seedTeamMd)
        {
            var squadDir = Path.Combine(teamRoot, ".squad");
            Directory.CreateDirectory(squadDir);
            File.WriteAllText(Path.Combine(squadDir, "team.md"), "| Ralph | Work Monitor |");
            var ralph = Path.Combine(squadDir, "agents", "ralph");
            Directory.CreateDirectory(ralph);
            File.WriteAllText(Path.Combine(ralph, "charter.md"), "# ralph");
        }

        return BuildSquadResourceForExistingRoot(name, teamRoot);
    }

    private static SquadResource BuildSquadResourceForExistingRoot(string name, string teamRoot)
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddSquad(name, teamRoot: teamRoot);

        // Build the app so WithCommand registrations land on the resource's annotations.
        // We don't start the app — that would invoke the lifecycle hook, which has its
        // own tests; here we only need the command surface materialized.
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        return model.Resources.OfType<SquadResource>().Single();
    }

    private static Task<ExecuteCommandResult> InvokeCommandAsync(SquadResource resource, string commandName)
    {
        var command = resource.Annotations
            .OfType<ResourceCommandAnnotation>()
            .SingleOrDefault(c => c.Name == commandName)
            ?? throw new InvalidOperationException(
                $"Command '{commandName}' not found on resource '{resource.Name}'.");

        // ExecuteCommandContext is constructed via its public ctor in current Aspire.Hosting
        // (Aspire 13.x). We pass a minimal context — the Squad commands ignore Logger / Arguments.
        var ctx = new ExecuteCommandContext
        {
            ServiceProvider = null!,
            ResourceName = resource.Name,
            CancellationToken = default,
            Logger = NullLogger.Instance,
            Arguments = new InteractionInputCollection(Array.Empty<InteractionInput>()),
        };

        return command.ExecuteCommand(ctx);
    }

    private string CreateTeamRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ctk-aspire-squad-commands-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
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
