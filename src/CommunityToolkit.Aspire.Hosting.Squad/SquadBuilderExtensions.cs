using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

#pragma warning disable ASPIREATS001 // AspireExport is experimental

namespace Aspire.Hosting;

/// <summary>
/// Provides fluent extension methods for adding and configuring Squad AI-agent team resources
/// in a .NET Aspire <see cref="IDistributedApplicationBuilder"/>.
/// </summary>
public static class SquadBuilderExtensions
{
    /// <summary>
    /// Adds a Squad AI-agent team to the distributed application.
    ///
    /// The lifecycle hook discovers agents from <c>.squad/team.md</c> (or the default roster when
    /// the file does not exist), publishes <c>Spawning</c> to <c>Active</c> state transitions visible in
    /// the Aspire dashboard, and injects a custom <c>squad://</c> descriptor that downstream
    /// services can consume as metadata. The Squad resource is a logical dashboard/resource-graph entry;
    /// it does not start a server process by itself.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/> to add the resource to.</param>
    /// <param name="name">The Aspire resource name.</param>
    /// <param name="teamRoot">
    /// Absolute path to the workspace root - the directory that contains the <c>.squad/</c> folder.
    /// Defaults to <see cref="Directory.GetCurrentDirectory"/> when not specified.
    /// </param>
    /// <returns>An <see cref="IResourceBuilder{SquadResource}"/> for further configuration.</returns>
    /// <example>
    /// <code lang="csharp">
    /// var builder = DistributedApplication.CreateBuilder(args);
    ///
    /// var squad = builder.AddSquad("research-squad",
    ///     teamRoot: @"C:\repos\my-project");
    ///
    /// builder.AddProject&lt;Projects.IncidentWorkflow&gt;("workflow")
    ///     .WithReference(squad);
    ///
    /// builder.Build().Run();
    /// </code>
    /// </example>
    [AspireExport]
    public static IResourceBuilder<SquadResource> AddSquad(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        string? teamRoot = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resolvedRoot = teamRoot ?? Directory.GetCurrentDirectory();

        var resource = new SquadResource(name, resolvedRoot);

        // Attach team annotation for lifecycle hook and dashboard properties.
        resource.Annotations.Add(new SquadTeamAnnotation(
            teamRoot: resolvedRoot,
            decisionsMdPath: Path.Combine(resolvedRoot, ".squad", "decisions.md"),
            inboxDir: Path.Combine(resolvedRoot, ".squad", "decisions", "inbox"),
            agentRosterFile: Path.Combine(resolvedRoot, ".squad", "team.md")));

        // Register the event subscriber (idempotent: only one singleton).
        builder.Services.TryAddEventingSubscriber<SquadLifecycleHook>();

        var resourceBuilder = builder.AddResource(resource)
            .WithInitialState(new CustomResourceSnapshot
            {
                ResourceType = "Squad",
                CreationTimeStamp = DateTime.UtcNow,
                State = new ResourceStateSnapshot("Configured", KnownResourceStateStyles.Info),
                Properties = [..SquadDashboardProperties.CreateStatic(resource)],
            });

        // Dashboard commands.
        // These commands appear as buttons in the Aspire dashboard "Commands"
        // column for the squad resource row.

        resourceBuilder.WithCommand(
            name: "refresh-agents",
            displayName: "Refresh Agents",
            executeCommand: ctx =>
            {
                if (resource.Annotations.OfType<SquadTeamAnnotation>().FirstOrDefault() is not { } annotation)
                    return Task.FromResult(new ExecuteCommandResult { Success = false, Message = "No team annotation found." });

                var rosterFile = annotation.AgentRosterFile;
                var count = resource.Agents.Count;
                var exists = File.Exists(rosterFile);
                var message = exists
                    ? $"Team roster at '{rosterFile}' lists {count} agent(s): {string.Join(", ", resource.Agents)}."
                    : $"No team.md found at '{rosterFile}'; using default roster of {count} agent(s).";

                // Nothing to update at runtime (agents are wired at start), but log the result.
                Console.WriteLine($"[Squad] {message}");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            new CommandOptions
            {
                Description = "Re-reads .squad/team.md and reports the current agent roster. Agents are registered at start-up; re-running the AppHost applies any roster changes.",
                IconName = "ArrowClockwise",
                IsHighlighted = true,
                UpdateState = _ => ResourceCommandState.Enabled,
            });

        resourceBuilder.WithCommand(
            name: "open-team-root",
            displayName: "Open Team Root",
            executeCommand: ctx =>
            {
                if (resource.Annotations.OfType<SquadTeamAnnotation>().FirstOrDefault() is not { } annotation)
                    return Task.FromResult(new ExecuteCommandResult { Success = false, Message = "No team annotation found." });

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = annotation.TeamRoot,
                        UseShellExecute = true,
                    });
                    return Task.FromResult(new ExecuteCommandResult { Success = true });
                }
                catch (Win32Exception ex)
                {
                    return Task.FromResult(new ExecuteCommandResult { Success = false, Message = ex.Message });
                }
                catch (FileNotFoundException ex)
                {
                    return Task.FromResult(new ExecuteCommandResult { Success = false, Message = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Task.FromResult(new ExecuteCommandResult { Success = false, Message = ex.Message });
                }
            },
            new CommandOptions
            {
                Description = "Opens the squad team root directory in the system file explorer.",
                IconName = "FolderOpen",
                UpdateState = _ => ResourceCommandState.Enabled,
            });

        resourceBuilder.WithCommand(
            name: "open-copilot-cli",
            displayName: "Open Copilot CLI",
            executeCommand: ctx =>
            {
                if (resource.Annotations.OfType<SquadTeamAnnotation>().FirstOrDefault() is not { } annotation)
                    return Task.FromResult(new ExecuteCommandResult { Success = false, Message = "No team annotation found." });

                var result = LaunchCopilotCli(resource.Name, annotation.TeamRoot);
                return Task.FromResult(result.Success
                    ? new ExecuteCommandResult { Success = true }
                    : new ExecuteCommandResult { Success = false, Message = result.Message });
            },
            new CommandOptions
            {
                Description = "Opens a terminal rooted at the squad workspace and starts GitHub Copilot CLI.",
                IconName = "Bot",
                UpdateState = _ => ResourceCommandState.Enabled,
            });

        resourceBuilder.WithCommand(
            name: "check-inbox",
            displayName: "Check Inbox",
            executeCommand: ctx =>
            {
                if (resource.Annotations.OfType<SquadTeamAnnotation>().FirstOrDefault() is not { } annotation)
                    return Task.FromResult(new ExecuteCommandResult { Success = false, Message = "No team annotation found." });

                var inboxDir = annotation.InboxDir;
                if (!Directory.Exists(inboxDir))
                {
                    Console.WriteLine($"[Squad] Inbox directory does not exist: {inboxDir}");
                    return Task.FromResult(new ExecuteCommandResult { Success = true });
                }

                var pending = Directory.GetFiles(inboxDir, "*.md");
                var summary = pending.Length == 0
                    ? "Inbox is empty - no pending decisions."
                    : $"{pending.Length} pending item(s): {string.Join(", ", pending.Select(Path.GetFileName))}";

                Console.WriteLine($"[Squad] {summary}");
                return Task.FromResult(new ExecuteCommandResult { Success = true });
            },
            new CommandOptions
            {
                Description = "Counts pending .md files in .squad/decisions/inbox/ and prints a summary to the AppHost console.",
                IconName = "Mail",
                UpdateState = _ => ResourceCommandState.Enabled,
            });

        return resourceBuilder;
    }

    private static LaunchResult LaunchCopilotCli(string squadName, string teamRoot)
    {
        if (!Directory.Exists(teamRoot))
        {
            return LaunchResult.Failed($"Squad team root was not found: {teamRoot}");
        }

        var windowTitle = $"Copilot - {squadName}";

        try
        {
            if (OperatingSystem.IsWindows())
            {
                LaunchCopilotCliWindows(squadName, teamRoot, windowTitle);
            }
            else if (OperatingSystem.IsMacOS())
            {
                LaunchCopilotCliMacOs(squadName, teamRoot);
            }
            else if (OperatingSystem.IsLinux())
            {
                if (!LaunchCopilotCliLinux(squadName, teamRoot))
                {
                    return LaunchResult.Failed(
                        "Could not open a terminal for GitHub Copilot CLI: no supported terminal emulator " +
                        "(x-terminal-emulator, gnome-terminal, konsole, xfce4-terminal, xterm) was found on PATH. " +
                        "Install one, or run 'copilot --agent squad' manually from the squad workspace.");
                }
            }
            else
            {
                return LaunchResult.Failed("Opening GitHub Copilot CLI is not supported on this operating system.");
            }

            return LaunchResult.Succeeded($"Opened GitHub Copilot CLI for squad '{squadName}'.");
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return LaunchResult.Failed($"Failed to open Copilot CLI terminal: {ex.Message}");
        }
    }

    // Windows PowerShell 5.1 misreads UTF-8-without-BOM content as ANSI, which corrupts any
    // non-ASCII characters embedded in the generated .ps1 (e.g. a teamRoot path inside
    // Set-Location -LiteralPath '…'). Emitting a BOM makes 5.1 decode the script as UTF-8.
    internal static readonly Encoding WindowsScriptEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    // Unix scripts must stay BOM-free: a leading BOM breaks the '#!/bin/bash' shebang. LF-only.
    internal static readonly Encoding UnixScriptEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // Copilot is always launched from a temporary SCRIPT FILE written under
    // <teamRoot>/.squad/.cache/. Running a file means no terminal — in particular
    // Windows Terminal, which treats ';' as a pane/command separator — ever has to
    // parse ';', '&', or quotes out of an inline command string, which is what caused
    // wt.exe to fail with Win32 error 0x80070002 (ERROR_FILE_NOT_FOUND).
    internal static string WriteLaunchScript(string teamRoot, string squadName, string extension, string content, bool makeExecutable, Encoding encoding)
    {
        var cacheDir = Path.Combine(teamRoot, ".squad", ".cache");
        Directory.CreateDirectory(cacheDir);

        // Unique per launch (timestamp + random token) so re-launching never contends with a
        // file that is still locked/open by a previously spawned terminal. The GUID guards against
        // coarse clock granularity (~15ms on Windows) producing duplicate timestamps for rapid launches.
        var fileName = $"launch-copilot-{SanitizeFileComponent(squadName)}-{DateTime.Now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var scriptPath = Path.Combine(cacheDir, fileName);

        // Windows scripts are written UTF-8 WITH BOM (see WindowsScriptEncoding); Unix scripts
        // UTF-8 WITHOUT BOM (see UnixScriptEncoding) so the shebang stays intact.
        File.WriteAllText(scriptPath, content, encoding);

        // Guarded so File.SetUnixFileMode is only invoked on Unix (it throws on Windows).
        if (makeExecutable && !OperatingSystem.IsWindows())
        {
            // rwxr-xr-x (0755)
            File.SetUnixFileMode(
                scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return scriptPath;
    }

    private static string SanitizeFileComponent(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(c => Array.IndexOf(invalid, c) >= 0 ? '-' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(cleaned) ? "squad" : cleaned;
    }

    internal static string BuildUnixLaunchScript(string teamRoot)
    {
        // bash single-quoted string escaping: ' -> '\''
        var escapedRoot = teamRoot.Replace("'", "'\\''");

        // Join with '\n' (not Environment.NewLine) so the script keeps LF endings on all hosts.
        return string.Join('\n',
            "#!/bin/bash",
            $"cd '{escapedRoot}'",
            "if command -v copilot >/dev/null 2>&1; then",
            "    copilot --agent squad",
            "else",
            "    echo 'GitHub Copilot CLI was not found on PATH. Install it or add copilot to PATH, then retry from Aspire.'",
            "fi",
            "exec \"${SHELL:-/bin/bash}\"") + "\n";
    }

    internal static string BuildWindowsLaunchScript(string teamRoot)
    {
        // PowerShell single-quoted string escaping: ' -> ''
        var escapedRoot = teamRoot.Replace("'", "''");

        // Newline-separated statements (Environment.NewLine) written to a .ps1 FILE and run with
        // -File. This is deliberately NOT a single ';'-joined inline command: passing such a string
        // to wt.exe made Windows Terminal treat ';' as a pane separator and fail with 0x80070002.
        return string.Join(
            Environment.NewLine,
            $"Set-Location -LiteralPath '{escapedRoot}'",
            "$copilot = Get-Command copilot -ErrorAction SilentlyContinue",
            "if ($copilot) { & $copilot.Source --agent squad } " +
            "else { Write-Host 'GitHub Copilot CLI was not found on PATH. Install it or add copilot to PATH, then retry from Aspire.' -ForegroundColor Yellow }") + Environment.NewLine;
    }

    // OS-bound spawn helpers — excluded from coverage because they hand off to terminal
    // processes. The launch itself can only be exercised in a user-driven smoke test.
    [ExcludeFromCodeCoverage]
    private static void LaunchCopilotCliWindows(string squadName, string teamRoot, string windowTitle)
    {
        var script = BuildWindowsLaunchScript(teamRoot);

        var scriptPath = WriteLaunchScript(teamRoot, squadName, ".ps1", script, makeExecutable: false, WindowsScriptEncoding);

        // Prefer Windows Terminal (wt.exe), running the script FILE with -File.
        try
        {
            var wt = new ProcessStartInfo
            {
                FileName = "wt.exe",
                UseShellExecute = false,
            };

            wt.ArgumentList.Add("-d");
            wt.ArgumentList.Add(teamRoot);
            wt.ArgumentList.Add("--title");
            wt.ArgumentList.Add(windowTitle);
            wt.ArgumentList.Add("powershell");
            wt.ArgumentList.Add("-NoLogo");
            wt.ArgumentList.Add("-NoExit");
            wt.ArgumentList.Add("-File");
            wt.ArgumentList.Add(scriptPath);

            Process.Start(wt);
            return;
        }
        catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
        {
            // Windows Terminal is not installed. Fall back to a plain console window.
        }

        // Fallback: cmd's built-in `start` opens a new console window. The first quoted
        // token after `start` becomes the window title.
        var fallback = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = teamRoot,
            UseShellExecute = false,
        };

        fallback.ArgumentList.Add("/c");
        fallback.ArgumentList.Add("start");
        fallback.ArgumentList.Add(windowTitle);
        fallback.ArgumentList.Add("powershell");
        fallback.ArgumentList.Add("-NoLogo");
        fallback.ArgumentList.Add("-NoExit");
        fallback.ArgumentList.Add("-File");
        fallback.ArgumentList.Add(scriptPath);

        Process.Start(fallback);
    }

    [ExcludeFromCodeCoverage]
    private static void LaunchCopilotCliMacOs(string squadName, string teamRoot)
    {
        var scriptPath = WriteLaunchScript(teamRoot, squadName, ".command", BuildUnixLaunchScript(teamRoot), makeExecutable: true, UnixScriptEncoding);

        var open = new ProcessStartInfo
        {
            FileName = "open",
            UseShellExecute = false,
        };

        open.ArgumentList.Add("-a");
        open.ArgumentList.Add("Terminal");
        open.ArgumentList.Add(scriptPath);

        Process.Start(open);
    }

    [ExcludeFromCodeCoverage]
    private static bool LaunchCopilotCliLinux(string squadName, string teamRoot)
    {
        var scriptPath = WriteLaunchScript(teamRoot, squadName, ".sh", BuildUnixLaunchScript(teamRoot), makeExecutable: true, UnixScriptEncoding);

        // Terminal emulators vary across distros; try the common ones in order and fall
        // through to the next when one is not installed (Win32Exception/FileNotFoundException).
        (string FileName, string[] Args)[] candidates =
        [
            ("x-terminal-emulator", ["-e", "bash", scriptPath]),
            ("gnome-terminal", ["--", "bash", scriptPath]),
            ("konsole", ["-e", "bash", scriptPath]),
            ("xfce4-terminal", ["-x", "bash", scriptPath]),
            ("xterm", ["-e", "bash", scriptPath]),
        ];

        foreach (var (fileName, args) in candidates)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    WorkingDirectory = teamRoot,
                    UseShellExecute = false,
                };

                foreach (var arg in args)
                {
                    startInfo.ArgumentList.Add(arg);
                }

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
            {
                // Emulator not present; try the next candidate.
            }
        }

        return false;
    }

    private sealed record LaunchResult(bool Success, string Message)
    {
        public static LaunchResult Succeeded(string message) => new(true, message);

        public static LaunchResult Failed(string message) => new(false, message);
    }
}
