using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

/// <summary>
/// Pure, host-independent assertions over the launch-script builders in
/// <see cref="SquadBuilderExtensions"/>. These run on every OS because they only inspect the
/// generated script string — they never spawn a terminal. They lock in the cross-platform fix
/// (a script FILE per OS, invoking <c>copilot --agent squad</c>) and guard against a regression
/// to the original inline <c>;</c>-joined command that made <c>wt.exe</c> fail with 0x80070002.
/// </summary>
public class SquadLaunchScriptTests
{
    // ─────────────────────────── Windows (.ps1) ───────────────────────────

    [Fact]
    public void BuildWindowsLaunchScript_InvokesCopilotWithAgentSquad()
    {
        var script = SquadBuilderExtensions.BuildWindowsLaunchScript(@"C:\repos\proj");

        Assert.Contains("--agent squad", script);
    }

    [Fact]
    public void BuildWindowsLaunchScript_SetsLocationToTeamRoot()
    {
        var script = SquadBuilderExtensions.BuildWindowsLaunchScript(@"C:\repos\proj");

        Assert.Contains(@"Set-Location -LiteralPath 'C:\repos\proj'", script);
    }

    [Fact]
    public void BuildWindowsLaunchScript_DoublesSingleQuoteInTeamRoot()
    {
        // PowerShell single-quoted string escaping: ' -> ''
        var script = SquadBuilderExtensions.BuildWindowsLaunchScript(@"C:\a'b");

        Assert.Contains(@"Set-Location -LiteralPath 'C:\a''b'", script);
    }

    [Fact]
    public void BuildWindowsLaunchScript_IsMultiLine_NotInlineSemicolonJoinedCommand()
    {
        // Regression guard for the original bug: the launch used to be a single
        // "Set-Location ...; $copilot = ...; if (...)" string handed to wt.exe, which treats ';'
        // as a pane separator and failed with Win32 0x80070002. The fix writes a multi-line .ps1
        // FILE run via -File, so no terminal ever parses ';'.
        var script = SquadBuilderExtensions.BuildWindowsLaunchScript(@"C:\repos\proj");

        var lines = script.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.True(lines.Length >= 3, $"Expected a multi-line script but got {lines.Length} line(s).");
        Assert.Equal(@"Set-Location -LiteralPath 'C:\repos\proj'", lines[0]);
        // No ';' statement separators anywhere — that was the failing inline pattern.
        Assert.DoesNotContain(";", script);
        Assert.DoesNotContain("wt.exe", script);
    }

    // ──────────────────────────── Unix (.sh/.command) ────────────────────────────

    [Fact]
    public void BuildUnixLaunchScript_InvokesCopilotWithAgentSquad()
    {
        var script = SquadBuilderExtensions.BuildUnixLaunchScript("/home/me/proj");

        Assert.Contains("copilot --agent squad", script);
    }

    [Fact]
    public void BuildUnixLaunchScript_CdsToTeamRoot()
    {
        var script = SquadBuilderExtensions.BuildUnixLaunchScript("/home/me/proj");

        Assert.Contains("cd '/home/me/proj'", script);
    }

    [Fact]
    public void BuildUnixLaunchScript_EscapesSingleQuoteInTeamRoot()
    {
        // bash single-quoted string escaping: ' -> '\''
        var script = SquadBuilderExtensions.BuildUnixLaunchScript("/home/m'e");

        Assert.Contains(@"cd '/home/m'\''e'", script);
    }

    [Fact]
    public void BuildUnixLaunchScript_UsesLfLineEndings()
    {
        var script = SquadBuilderExtensions.BuildUnixLaunchScript("/home/me/proj");

        Assert.DoesNotContain("\r", script);
        Assert.StartsWith("#!/bin/bash\n", script);
    }
}
