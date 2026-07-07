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

    // ──────────────────────────── Encoding / BOM (FIX 1) ────────────────────────────

    [Fact]
    public void WriteLaunchScript_WindowsScript_StartsWithUtf8Bom()
    {
        // Windows PowerShell 5.1 misreads UTF-8-without-BOM as ANSI, corrupting a non-ASCII
        // teamRoot embedded in Set-Location. The Windows .ps1 must be written UTF-8 WITH BOM.
        var root = CreateTempRoot();
        try
        {
            var content = SquadBuilderExtensions.BuildWindowsLaunchScript(root);
            var path = SquadBuilderExtensions.WriteLaunchScript(
                root, "squad", ".ps1", content, makeExecutable: false, SquadBuilderExtensions.WindowsScriptEncoding);

            var bytes = File.ReadAllBytes(path);

            Assert.True(bytes.Length >= 3, "Script file is unexpectedly small.");
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void WriteLaunchScript_UnixScript_HasNoBom()
    {
        // Unix scripts must stay BOM-free: a leading BOM breaks the #!/bin/bash shebang.
        var root = CreateTempRoot();
        try
        {
            var content = SquadBuilderExtensions.BuildUnixLaunchScript(root);
            var path = SquadBuilderExtensions.WriteLaunchScript(
                root, "squad", ".sh", content, makeExecutable: false, SquadBuilderExtensions.UnixScriptEncoding);

            var bytes = File.ReadAllBytes(path);

            Assert.True(bytes.Length >= 3, "Script file is unexpectedly small.");
            var hasBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            Assert.False(hasBom, "Unix script must not start with a UTF-8 BOM.");
            // Shebang must be the very first bytes so the kernel can exec the interpreter.
            Assert.Equal((byte)'#', bytes[0]);
            Assert.Equal((byte)'!', bytes[1]);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void WindowsScriptEncoding_EmitsUtf8Bom_UnixEncodingDoesNot()
    {
        Assert.Equal(3, SquadBuilderExtensions.WindowsScriptEncoding.GetPreamble().Length);
        Assert.Empty(SquadBuilderExtensions.UnixScriptEncoding.GetPreamble());
    }

    // ─────────────────────────────── Helpers ───────────────────────────────

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "squad-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; ignore.
        }
    }
}
