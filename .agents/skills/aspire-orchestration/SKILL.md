---
name: aspire-orchestration
description: >-
  **WORKFLOW SKILL** — Manage Aspire lifecycle in VS Code or the CLI.
  WHEN: "start or stop my Aspire app", "aspire_apphost_start",
  "aspire_apphost_stop", "notEditorOwned", "ambiguousSession", "aspire start",
  "aspire stop", "aspire wait", resource restart, file-lock errors (MSB3491 or
  CS2012), port conflicts, git worktrees, "--isolated", "aspire update --self",
  "aspire update --migrate", "aspire describe --include-hidden", "aspire stop --force",
  "aspire terminal", integration discovery, default watch, or hot reload.
  INVOKES: VS Code lifecycle tools first when exposed; Aspire CLI for readiness,
  inspection, resource operations, isolated worktree starts, and allowed fallbacks.
  DO NOT USE FOR: deploy/publish/destroy (aspire-deployment), logs/traces/metrics
  (aspire-monitoring), or AppHost code and resource wiring (aspireify).
  FOR SINGLE OPERATIONS: Load the matching editor tool first, then obey the
  exact-target, worktree-isolation, and stop-result rules below.
license: MIT
metadata:
  author: Microsoft
  version: "0.0.3"
---

# Aspire Orchestration

> **MANDATORY COMPLIANCE** — This skill prevents agent self-harm in Aspire projects.
> Violating these rules causes file locks, orphaned processes, and user frustration ([#15801](https://github.com/microsoft/aspire/issues/15801)).

## Prerequisites

| Requirement | Install |
|-------------|---------|
| .NET 10.0 SDK | https://dotnet.microsoft.com/download |
| Aspire CLI (curl/PowerShell) | `curl -sSL https://aspire.dev/install.sh \| bash` |
| Aspire CLI (npm) | `npm install -g @microsoft/aspire-cli` |
| Aspire CLI (NativeAOT global tool, .NET 10) | `dotnet tool install -g Aspire.Cli` |

Use the installation method owned by the user's environment. npm, Nix, Homebrew, WinGet,
mise, and the install scripts are supported alongside the NativeAOT .NET global tool.

## Detection

Activate when ANY signal is present:

| Signal | How to Detect | Confidence |
|--------|---------------|------------|
| C# AppHost | `.csproj` containing `Aspire.AppHost.Sdk` | ✅ Definitive |
| File-based C# AppHost | `apphost.cs` or `.cs` file with `#:sdk Aspire.AppHost.Sdk` | ✅ Definitive |
| TypeScript AppHost | Current `apphost.mts` or legacy `apphost.ts` file in project | ✅ Definitive |
| Aspire config | `aspire.config.json` in project root | High |
| Aspire settings | `.aspire/` directory present | High |
| Generated TS modules | `.aspire/modules/` directory present | High |
| Service defaults | `Aspire.ServiceDefaults` in project references | Medium |

See [detection.md](references/detection.md) for detailed fingerprinting.

## VS Code AppHost Lifecycle

When the agent host exposes `aspire_apphost_start` or `aspire_apphost_stop`, use the
matching tool before running `aspire start` or `aspire stop` in a terminal, except when
starting from a git worktree requires `--isolated` and the tool cannot request it. If the
tool is listed as deferred, load its contract with the host's tool-discovery mechanism
first; do not treat an unloaded deferred tool as unavailable.

Pass the exact selected `appHostPath` discovered by Aspire to editor lifecycle tools. In a
multi-root workspace, that tool contract may require a selector such as
`repo-a~1/MyApp.AppHost/MyApp.AppHost.csproj`.

The CLI `--apphost` flag does not understand that selector namespace. Before any CLI
start/stop fallback, resolve the selected AppHost to its actual filesystem project path
and pass that path to `--apphost`; never copy a multi-root selector verbatim. That CLI
project path may be workspace-relative (`MyApp.AppHost/MyApp.AppHost.csproj`) or
absolute (`/workspaces/repo-a/MyApp.AppHost/MyApp.AppHost.csproj` or
`C:\workspaces\repo-a\MyApp.AppHost\MyApp.AppHost.csproj`); use the current platform's
native path syntax. If several AppHosts are discovered and the user's target is unclear,
ask which one to use instead of guessing, invoking the tool for every AppHost, or
issuing an unscoped CLI command.

If the selected `appHostPath` is already a normal workspace-relative project path such
as `MyApp.AppHost/MyApp.AppHost.csproj`, reuse it unchanged for CLI fallbacks; do not
convert it to an absolute path just for the CLI.

**An unclear target is a hard stop.** Ask one clarifying question and wait for the user to
name an AppHost. Do not call a lifecycle tool or terminal command until the target is
resolved, and do not offer commands that bypass this gate. Stop multiple AppHosts only
when the user explicitly requests all of them.

For starts, call `aspire_apphost_start` with mode `run` and the exact selected
`appHostPath` unless the user explicitly asks to attach a debugger. In a git worktree, the
editor tool does not currently request Aspire's isolated state. Resolve the selected
AppHost to its filesystem path and use
`aspire start --non-interactive --isolated --apphost <filesystem-path>` instead.

`run` mode has no debugger attached, but it still has an editor-owned Aspire session that
`aspire_apphost_stop` can stop. After a stop call, follow this result matrix exactly:

| Tool result | Next action | CLI stop allowed? |
|-------------|-------------|-------------------|
| `stopped` or `notRunning` | Report the result; take no further stop action | No |
| `alreadyStopping`, controller `editor` | Report that the editor stop is already in progress; take no further stop action | No |
| `alreadyStarting`, controller `editor` | Retry `aspire_apphost_stop` once; if it repeats, report that startup is still in progress and take no further stop action | No |
| `notEditorOwned`, controller `external` | If the user requested that exact AppHost be stopped, resolve it to its filesystem path and run `aspire stop --non-interactive --apphost <filesystem-path>` | Yes |
| `failed`, controller `unknown` | Retry `aspire_apphost_stop` once; if the same result repeats, use the same exact-target CLI command above | Only after the retry |
| `ambiguousSession` | Stop nothing and have the user disambiguate in the editor | **Never** |
| Any other refusal or failure | Resolve or report that result; do not change mechanisms | No |

The rows are mutually exclusive. Act only on the current result; do not offer a command
from another row as a speculative future workaround. Re-evaluate only after a new tool
result is returned.

**`ambiguousSession` is a terminal safety refusal.** Do not run or offer a CLI fallback,
and do not ask whether the user wants one. User confirmation cannot make an ambiguous
editor session safe to terminate from the CLI.

Use direct Aspire CLI lifecycle commands only when the matching editor tool is unavailable,
for isolated worktree starts, or for a stop result explicitly marked as allowed above.
When a CLI fallback is allowed, keep the target exact by resolving the selected AppHost to
its filesystem path first.

## Safety Guardrails

| Situation | ✅ ALWAYS Do | ❌ NEVER Do |
|-----------|-------------|------------|
| Start an Aspire app | `aspire_apphost_start` with mode `run` and the exact selected `appHostPath` when available; in a git worktree, use `aspire start --non-interactive --isolated --apphost <filesystem-path>` even when `aspire_apphost_start` is available | `dotnet run` on AppHost |
| Wait for resource ready | `aspire wait <resource>` | `curl` / HTTP polling loops |
| Code changed in a resource | Prefer resource commands, runtime watch/HMR, dashboard actions, or IDE-managed debugging | `dotnet build` against locked files |
| Task complete | `aspire_apphost_stop` with the exact selected `appHostPath` when available; follow its result matrix | Use an unapproved CLI fallback |
| Check running AppHosts | `aspire ps` | Manual process inspection |
| Check resource status | `aspire describe` | `aspire ps --resources` (removed in 13.5) |
| Remove persistent resources | Confirm data loss and exact AppHost before `aspire stop --force --apphost <filesystem-path>` | Combining `--force` with `--all`, or using it for an ordinary stop |
| Working in git worktree | `aspire start --non-interactive --isolated --apphost <filesystem-path>` | `aspire_apphost_start` when it cannot request isolation |
| Running from AI agent | Load available lifecycle tools first; resolve CLI `--apphost` fallbacks to `<filesystem-path>`; add `--non-interactive` | Assuming interactive terminal |
| Editing unfamiliar API | `aspire docs search <topic>` then `aspire docs api search <query>` for API reference | Guessing API shape |
| C# AppHost API inspection | Use `dotnet-inspect` skill (if available) for local symbols | Guessing overloads or builder chains |
| Adding custom dashboard/resource commands | `aspire docs search "custom resource commands"` first | Inventing `WithCommand` patterns without docs |
| Installing Aspire support | Use `aspire add` or `aspire init` | ~~`dotnet workload install aspire`~~ (obsolete) |

See [safety-guardrails.md](references/safety-guardrails.md) for detailed rules and recovery patterns.

## Default Workflow

1. Confirm workspace is Aspire — identify the AppHost
2. If the workspace is a git worktree, resolve the selected AppHost to a filesystem project path and use `aspire start --non-interactive --isolated --apphost <filesystem-path>` even when `aspire_apphost_start` is available, because the editor tool cannot request isolation. Otherwise, start with `aspire_apphost_start` in mode `run` using the exact selected `appHostPath` when available; if that tool is unavailable, resolve the selected AppHost to a filesystem project path and use `aspire start --non-interactive --apphost <filesystem-path>`
3. `aspire wait <resource>` before interacting with any resource
4. `aspire describe` to inspect state, then work
5. If AppHost code changed, restart through the same lifecycle routing; if only one resource changed, prefer the resource's commands/watch/HMR/debug workflow
6. Stop with `aspire_apphost_stop` using the exact selected `appHostPath` when available; use `aspire stop --non-interactive --apphost <filesystem-path>` only for the documented fallback outcomes

## Quick Reference

| Task | Command |
|------|---------|
| Start app (agents) | Git worktree: `aspire start --non-interactive --isolated --apphost <filesystem-path>` even if `aspire_apphost_start` is available, because the tool cannot request isolation. Otherwise: `aspire_apphost_start` (mode `run`, exact selected `appHostPath`); CLI fallback when the tool is unavailable: `aspire start --non-interactive --apphost <filesystem-path>` |
| Start app (human) | `aspire run` (foreground, dashboard) |
| Stop app | `aspire_apphost_stop` (exact selected `appHostPath`); result-matrix fallback: `aspire stop --non-interactive --apphost <filesystem-path>` |
| Wait for resource | `aspire wait <resource>` |
| List running AppHosts | `aspire ps` |
| Check resource status | `aspire describe` |
| Show hidden resources (proxies, helpers, migrations) | `aspire describe --include-hidden` |
| Resource operation | `aspire resource <resource-name> <command>` such as `stop`, `start`, or `rebuild` when exposed |
| Discover resource commands | `aspire resource <resource-name> --help` |
| Attach to an experimental resource terminal | Enable `features.terminalCommandsEnabled`, then use `aspire terminal ps` / `aspire terminal attach` |
| Create new project | `aspire new aspire-starter` |
| Add Aspire to existing | `aspire init` (then hand off to `aspireify` skill for wiring) |
| Add integration | `aspire add <package>` |
| Discover integrations | `aspire integration list --format Json` / `aspire integration search <query> --format Json` |
| Upgrade the CLI itself | `aspire update --self` (managed installs may print the npm/.NET/Nix update command) |
| Update project package refs | `aspire update --yes --non-interactive` after approval |
| Migrate legacy TypeScript entry point | Explain that package references, config, tsconfig, imports, and the entry point all change; after approval for the package update and migration, run `aspire update --migrate --yes --non-interactive` |
| Restore generated files | `aspire restore` |
| Environment maintenance | `aspire cache clear`, `aspire certs trust`, `aspire certs clean` |
| Diagnose environment | `aspire doctor` |
| Machine-readable output | `--format Json` (supported: `ps`, `describe`, `start`) |
| Look up API reference | `aspire docs api search <query> --language csharp\|typescript` |
| Browse API entries | `aspire docs api list <scope>` |
| Get API detail | `aspire docs api get <id>` |

## Error Handling

| Symptom | Cause | Action |
|---------|-------|--------|
| **File lock errors during build (`MSB3491`, `CS2012`)** | **Aspire is running and holds locks on `bin/`, `obj/`, and assemblies.** | **Stop the AppHost through the lifecycle routing above**, then rebuild or restart it. Do NOT conclude the project has a permanent build failure. |
| "Port already in use" | Previous instance running | Stop, then restart through the lifecycle routing above |
| Resource not found | App not started or name wrong | Use `aspire ps` to find the AppHost, then `aspire describe` to check resources |
| Build errors in resource | Code error, not Aspire issue | Fix code, then use resource commands/watch/HMR/debug workflow or restart through the lifecycle routing if AppHost code changed |
| Environment issues | Missing SDK or tools | `aspire doctor` to diagnose |
| JSON parse failure from `aspire start` | Mixed human/JSON output ([#15843](https://github.com/microsoft/aspire/issues/15843)) | Strip non-JSON lines before parsing |
| `aspire ps` hangs | AppHost on breakpoint ([#15576](https://github.com/microsoft/aspire/issues/15576)) | Use timeout, check AppHost process |
| `aspire agent init` fails | Non-interactive terminal ([#16264](https://github.com/microsoft/aspire/issues/16264)) | Run from standard terminal |
| Docker daemon unavailable | Container-backed resources fail to start | Start Docker Desktop, then restart through the lifecycle routing above |
| Multiple AppHosts detected | Wrong AppHost targeted | Use `--apphost <filesystem-path>` to specify explicitly |

### 🔒 File-Lock Recovery (MSB3491 / CS2012) — Always Stop the AppHost First

When a build fails with `error MSB3491: Could not write to output file ...` or
`error CS2012: Cannot open ... for writing`, the project itself is healthy —
**Aspire is running and holding file locks** on the resource's output assemblies.
The recovery is always the same:

Resolve the exact AppHost and call `aspire_apphost_stop` first when available. If a CLI
fallback is permitted, resolve the selected AppHost to its filesystem path before using
the commands below; `ambiguousSession` and all other no-fallback results end the recovery
attempt.

```bash
# ✅ CLI stop only after the result matrix permits fallback
aspire stop --non-interactive --apphost <filesystem-path>  # release the locks
# ... then either rebuild / restart one resource if the resource exposes commands ...
aspire resource <name> rebuild   # example: C# project resource with rebuild command
# ... or restart the whole AppHost through aspire_apphost_start. If unavailable:
aspire start --non-interactive --apphost <filesystem-path>
# In a git worktree:
aspire start --non-interactive --isolated --apphost <filesystem-path>
```

| ❌ NEVER do | ✅ ALWAYS do |
|------------|-------------|
| Tell the user the project has a permanent build failure | Recognize the lock as Aspire holding outputs and stop the AppHost through lifecycle routing |
| `dotnet build` again with locks held | Stop the AppHost first, then `dotnet build` (or prefer resource commands/watch/HMR/debug workflow) |
| Delete `bin/` / `obj/` to "fix" the lock | Stop the AppHost; deletion may succeed but the next build relocks |
| `pkill dotnet` or `kill <PID>` to free locks | Use the editor stop tool or exact-target CLI fallback for clean shutdown |
| Tell the user to "reboot" or "restart your machine" | Stop the AppHost through lifecycle routing |

The same rule applies to any "file in use", "cannot access the file", or
"another process is using" error during a build of an Aspire-managed resource.

## Handoff Rules

| Scenario | Route To |
|----------|----------|
| AppHost wiring after `aspire init` (scan repo, add resources, ServiceDefaults/OTel) | → `aspireify` skill ([`aspireify/SKILL.md`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspireify/SKILL.md)) or project-local `.agents/skills/aspireify/SKILL.md` |
| Browser logs (`Aspire.Hosting.Browsers` / `WithBrowserLogs()`) and dashboard authoring | → `aspireify` skill (code edits) and `aspire-monitoring` (discovery) |
| Custom resource commands, arguments, interactions, or terminals (`WithCommand`, `CommandOptions.Arguments`, `IInteractionService`, `WithTerminal`) | → `aspireify` skill |
| Lifecycle hooks (`SubscribeBeforeStart`, `SubscribeAfterResourcesCreated`, BeforeStart pipeline phase) | → `aspireify` skill |
| Endpoint authoring (`WithEndpoint` updates, `ExcludeReferenceEndpoint` flag) | → `aspireify` skill |
| Deploy, publish, pipeline steps, `aspire destroy` | → `aspire-deployment` skill |
| Logs, traces, metrics, dashboard, `aspire dashboard run` | → `aspire-monitoring` skill |
| Deployed app diagnostics | → `azure-diagnostics` skill (azure-skills) |

## Runtime Settings And Environment

| Variable | Default | Purpose |
|----------|---------|---------|
| `ASPIRE_ENABLE_CONTAINER_TUNNEL` | `true` | Container tunnel provides uniform host connectivity across Docker Desktop, Docker Engine, and Podman. Set to `false` to opt out. |
| `ASPIRE_ENVIRONMENT` | unset | Selects the environment-specific config profile — controls which `appsettings.{environment}.json` is loaded and which environment is reported in dashboard telemetry. |
| `ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE` | `true` | The Aspire trusted developer certificate is used by DCP on Windows. Set to `false` to opt out. |
| `features.defaultWatchEnabled` | false unless configured | Enables Aspire default watch for supported C# and TypeScript AppHosts. Do not treat this as per-resource rebuild, restart, or hot reload for resource source changes. |

## TypeScript AppHost Note

Detection covers current `apphost.mts` and legacy `apphost.ts`, but **all TS AppHost
authoring is delegated to `aspireify`**. The CLI-driven legacy migration is project
maintenance owned by this orchestration skill, not authoring. Explain that it also updates
Aspire packages, config, tsconfig, and imports; offer
`aspire update --migrate --yes --non-interactive` only after approval for the full change,
then hand back to aspireify only if source authoring remains.
Current rules to apply when handing off:

| Rule | Why |
|------|-----|
| Prefer unified `withEnvironment(name, value)` over deprecated per-kind helpers (`withEnvironmentEndpoint`, `withEnvironmentParameter`, `withEnvironmentConnectionString`, `withEnvironmentExpression`, `withEnvironmentFromOutput`, `withEnvironmentFromKeyVaultSecret`) | Per-kind helpers are deprecated — single API now handles all value types |
| Never edit `.aspire/modules/` directly | Generated; use `aspire add <package>` to regenerate and `aspire restore` to recover missing files |
| Use `aspire docs api search <query> --language typescript` for API lookup | TS surface differs from C# |

## Skill Routing — In-Plugin Sibling Skills

After `aspire init` drops a skeleton AppHost + `aspire.config.json`, route AppHost wiring
(scan repo → propose resource graph → edit AppHost → wire `Aspire.ServiceDefaults` / OTel →
validate via `aspire start`) to the in-plugin **aspireify** skill: [`aspireify/SKILL.md`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspireify/SKILL.md).
For first-run flows that only need the skeleton drop, see the in-plugin **aspire-init** skill:
[`aspire-init/SKILL.md`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-init/SKILL.md). This orchestration skill stays focused
on lifecycle (start/stop/wait/restart) and never edits AppHost code itself.

## Project-Local Skill Precedence

If `.agents/skills/aspire/SKILL.md` exists (from `aspire agent init`), defer to it for:
C# AppHost editing, TS AppHost editing, Playwright handoff, investigation workflows.
Safety guardrails from this plugin ALWAYS apply.

If `.agents/skills/aspireify/SKILL.md` exists project-locally (installed by `aspire init` in
current Aspire), **warn the user** that a project-local aspireify skill is present and **defer to it**
for AppHost wiring instead of the in-plugin sibling. Same precedence rule as the project-local
`aspire` skill above: project-local wins, plugin guardrails still apply.

## References

- [safety-guardrails.md](references/safety-guardrails.md) — Detailed rules and recovery patterns
- [detection.md](references/detection.md) — Project fingerprinting
- [app-commands.md](references/app-commands.md) — App lifecycle and bootstrap commands
- [resource-management.md](references/resource-management.md) — Resource wait, restart, and operations
- [agent-workflows.md](references/agent-workflows.md) — Common agent investigation, integration, TypeScript, and handoff workflows
