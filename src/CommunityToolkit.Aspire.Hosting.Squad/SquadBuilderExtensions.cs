using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

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
        if (!OperatingSystem.IsWindows())
        {
            return LaunchResult.Failed("Opening an interactive Copilot CLI terminal is currently implemented for Windows AppHost sessions only.");
        }

        if (!Directory.Exists(teamRoot))
        {
            return LaunchResult.Failed($"Squad team root was not found: {teamRoot}");
        }

        var windowTitle = $"Copilot - {squadName}";
        var copilotCommand = "$copilot = Get-Command copilot -ErrorAction SilentlyContinue; " +
            "if ($copilot) { & $copilot.Source } " +
            "else { Write-Host 'GitHub Copilot CLI was not found on PATH. Install it or add copilot to PATH, then retry from Aspire.' -ForegroundColor Yellow }";

        try
        {
            StartWindowsTerminal(teamRoot, windowTitle, copilotCommand);
            return LaunchResult.Succeeded($"Opened GitHub Copilot CLI for squad '{squadName}'.");
        }
        catch (Win32Exception)
        {
            // Windows Terminal is optional. Fall back to the inbox console host.
        }
        catch (FileNotFoundException)
        {
            // Windows Terminal is optional. Fall back to the inbox console host.
        }
        catch (InvalidOperationException)
        {
            // Windows Terminal is optional. Fall back to the inbox console host.
        }

        try
        {
            StartConsoleWindow(teamRoot, windowTitle, copilotCommand);
            return LaunchResult.Succeeded($"Opened GitHub Copilot CLI for squad '{squadName}' in a PowerShell window.");
        }
        catch (Win32Exception ex)
        {
            return LaunchResult.Failed($"Failed to open Copilot CLI terminal: {ex.Message}");
        }
        catch (FileNotFoundException ex)
        {
            return LaunchResult.Failed($"Failed to open Copilot CLI terminal: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return LaunchResult.Failed($"Failed to open Copilot CLI terminal: {ex.Message}");
        }
    }

    // OS-bound spawn helpers — excluded from coverage because they hand off to wt.exe /
    // powershell.exe which open real terminal windows. Their argument-list building is
    // simple enough to read at a glance; the launch itself can only be exercised in a
    // user-driven smoke test on a Windows desktop.
    [ExcludeFromCodeCoverage]
    private static void StartWindowsTerminal(string workingDirectory, string windowTitle, string copilotCommand)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "wt.exe",
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(workingDirectory);
        startInfo.ArgumentList.Add("--title");
        startInfo.ArgumentList.Add(windowTitle);
        startInfo.ArgumentList.Add("powershell.exe");
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoExit");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(copilotCommand);

        Process.Start(startInfo);
    }

    [ExcludeFromCodeCoverage]
    private static void StartConsoleWindow(string workingDirectory, string windowTitle, string copilotCommand)
    {
        // Launch PowerShell directly with UseShellExecute=true (which opens a new console window
        // for non-console parents) instead of going through `cmd /c start "<title>" ...`.
        // `start` treats the first quoted argument as the window title, but cmd's quote-stripping
        // rules make passing a title that contains spaces (e.g., "Copilot - research-squad")
        // fragile — the title can be swallowed into the command. Setting the title inside the new
        // shell via $Host.UI.RawUI.WindowTitle removes that fragility entirely.
        var escapedTitle = windowTitle.Replace("'", "''");
        var inlineCommand = $"$Host.UI.RawUI.WindowTitle = '{escapedTitle}'; {copilotCommand}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
        };

        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoExit");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(inlineCommand);

        Process.Start(startInfo);
    }

    private sealed record LaunchResult(bool Success, string Message)
    {
        public static LaunchResult Succeeded(string message) => new(true, message);

        public static LaunchResult Failed(string message) => new(false, message);
    }
}
