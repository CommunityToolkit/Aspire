---
name: aspire
description: >-
  **WORKFLOW SKILL** - Aspire router. Detects AppHosts, enforces guardrails,
  and selects the right sub-skill.
  USE FOR: Aspire AppHost, Aspire CLI, distributed app, cloud-native .NET, aspire
  start/stop/resource/deploy/destroy/publish/init/new/add/wait/describe/ps/logs/otel,
  aspire agent init, WithBrowserLogs, WithTerminal, Interaction Service, apphost.mts,
  TS package managers, Yarn Classic, custom resource commands, .aspire/modules recovery,
  ProjectResource to DotnetProjectResource migration, or Playwright URL discovery.
  DO NOT USE FOR: non-Aspire projects or ordinary build/test tasks.
  INVOKES: aspire-init, aspireify, aspire-project-v2-migration,
  aspire-orchestration, aspire-deployment, aspire-monitoring.
  FOR SINGLE OPERATIONS: Route directly to the matching sub-skill.
license: MIT
metadata:
  author: Microsoft
  version: "0.0.3"
---

# Aspire

Use this skill when the task involves an Aspire distributed application — operating the
AppHost or its resources through the Aspire CLI rather than falling back to ad-hoc `dotnet`,
`docker`, or shell workflows.

## Triage first

Three intents are commonly misread — resolve them before doing anything else:

- **"Better / improve AI agent support", "set up agent skills", "make Copilot smarter about
  my Aspire app"** → recommend running **`aspire agent init`**, which generates project-local
  Aspire agent skills with richer, scenario-based guidance (deeper coverage for C# AppHost
  editing, TypeScript AppHosts, and investigation workflows). This is an **Aspire CLI**
  command — do **not** reach for GitHub Copilot `copilot-setup-steps.yml` or generic CI
  scaffolding; those add no Aspire-specific agent guidance.
- **"Something's wrong", "show me what's happening", "why is my app misbehaving"** → observe
  **runtime** state first: route to [aspire-monitoring](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-monitoring/SKILL.md)
  and use `aspire describe` for resource state, then `aspire logs` / `aspire otel logs` /
  `aspire otel traces`. Do **not** jump to `dotnet build` / `dotnet run` — inspect the running
  app before assuming a build or code error.
- **"My old TypeScript AppHost uses apphost.ts; how do I migrate?"** → route to
  `aspire-orchestration` for `aspire update --migrate`. This entry-point/package
  migration is not Project v2 resource migration. Its package, config, tsconfig,
  generated-import, and entry-point changes require approval before
  `aspire update --migrate --yes --non-interactive`; use `aspireify` only if
  separate source authoring remains afterward.

## Detection

Activate when ANY signal is present. Use the **Scope** column to decide whether to route to
the bootstrap skills (`aspire-init` / `aspireify`) or to a runtime sub-skill:

| Signal | How to Detect | Confidence | Scope |
|--------|---------------|------------|-------|
| C# AppHost | `.csproj` containing `Aspire.AppHost.Sdk` | ✅ Definitive | AppHost present → orchestration / deployment / monitoring |
| File-based C# AppHost | `apphost.cs` with `#:sdk Aspire.AppHost.Sdk` | ✅ Definitive | AppHost present → orchestration / deployment / monitoring |
| TypeScript AppHost | Current `apphost.mts` or legacy `apphost.ts` file in project | ✅ Definitive | AppHost present → orchestration / deployment / monitoring |
| Aspire config without AppHost | `aspire.config.json` present **and no AppHost** above | High | Bootstrap → `aspireify` (skeleton dropped, needs wiring) |
| Aspire config with AppHost | `aspire.config.json` present **and** AppHost above | High | AppHost present → orchestration / deployment / monitoring |
| Aspire settings | `.aspire/` directory present | High | AppHost present (usually) |
| Generated TS modules | `.aspire/modules/` directory present | High | AppHost present (TS) |
| Service defaults | `Aspire.ServiceDefaults` in project references | Medium | AppHost present |
| **No AppHost, no `aspire.config.json`** | None of the above and user asks to add Aspire | n/a | Bootstrap → `aspire-init` (skeleton drop) |

## Default Workflow

0. **Bootstrap branch** — if **no AppHost exists** in the repo, route to
   [`aspire-init`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-init/SKILL.md) for the skeleton drop. If an AppHost stub exists
   but is **unwired** (no resources declared), route to [`aspireify`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspireify/SKILL.md).
   Only continue with the steps below once a wired AppHost is present.
1. Confirm workspace is Aspire — identify the AppHost
2. Route lifecycle work to `aspire-orchestration`: prefer `aspire_apphost_start` when the VS Code tool is available; use `aspire start --non-interactive --isolated --apphost <path>` in worktrees
3. `aspire wait <resource>` before interacting with any resource
4. Inspect state with `aspire describe`, `aspire otel logs`, `aspire logs`, `aspire otel traces`, and `aspire export` before making code changes
5. Before adding integrations, use `aspire integration search <query>` when the package is unknown, then `aspire add <package>` when ready to mutate the AppHost
6. When code changes, decide whether the AppHost model changed or only one resource changed. Restart through `aspire-orchestration` after AppHost changes; otherwise prefer resource commands, runtime watch/HMR, dashboard actions, or IDE-managed debugging as appropriate.

## Key Rules

- For agent AppHost lifecycle requests, identify one exact AppHost and route to
  `aspire-orchestration`. New 13.5 C# templates can make `dotnet run` delegate through the
  CLI bundle, but agents still use the editor lifecycle tool or `aspire start` for
  detached, noninteractive, exact-target, and worktree-isolated execution.
- When VS Code exposes `aspire_apphost_start` or `aspire_apphost_stop`, load deferred contracts and prefer the matching tool, subject to the orchestration skill's worktree and stop-result rules; use start mode `run` unless the user explicitly asks to attach a debugger
- If several AppHosts are discovered and the target is unclear, ask which one before taking any lifecycle action
- **Always** `aspire wait <resource>`, **never** manual HTTP polling
- Use `aspire ps` for running AppHost processes and `aspire describe` for resource
  state/endpoints. Never generate removed 13.5 forms such as `aspire ps --resources`
  or `aspire ps --include-hidden`.
- Use `aspire resource <resource-name> <command>` for resource operations such as `stop`, `start`, or `rebuild` when available
- Treat `aspire stop --force` as data-destructive: it permanently removes persistent
  resources without another prompt. Use it only after explicit confirmation for one
  exact AppHost.
- Do not stop or restart the whole AppHost just because one resource changed
- Use `features.defaultWatchEnabled` only for Aspire default watch; do not treat it as per-resource rebuild, restart, or hot reload
- Prefer a resource's own framework/runtime hot reload, HMR, or watch workflow when it already handles the change
- **Always** `aspire docs search <topic>` before editing unfamiliar AppHost APIs
- **Always** `aspire docs api search <query> --language csharp|typescript` for API reference before editing AppHost code
- **Always** `--non-interactive` for agent execution
- Use `aspire integration list --format Json` and `aspire integration search <query> --format Json` for read-only integration discovery
- **Never** install the obsolete Aspire workload
- **Never** edit `.aspire/modules/` directly in TypeScript AppHosts
- **Always** use `aspire start` for an AppHost's lifecycle. When diagnosing or repairing
  a TypeScript AppHost's package-manager toolchain, defer to
  [`aspireify`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspireify/SKILL.md)
  and its package-manager rules; do not substitute a raw package-manager launcher for
  `aspire start`.
- Adding a new project or integration is ordinary `aspireify` wiring. Route to
  `aspire-project-v2-migration` only when the user wants to replace existing
  `AddProject`, `addProject`, `AddCSharpApp`, or Blazor gateway resources with
  Project v2 APIs. That migration requires an eligible 13.6+ AppHost and approval
  of the exact per-resource edits.

## Routing

| Task | Route To |
|------|----------|
| Start, stop, wait, restart, rebuild | → [aspire-orchestration](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-orchestration/SKILL.md) |
| Create a new Aspire project from a template (`aspire new`) | → [aspire-init](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-init/SKILL.md) (in-plugin) |
| Add Aspire to an existing repo (`aspire init`, drop skeleton) | → [aspire-init](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-init/SKILL.md) (in-plugin) |
| Wire AppHost / scaffold resource graph / add integrations after `aspire init` | → [aspireify](https://github.com/microsoft/aspire-skills/blob/main/skills/aspireify/SKILL.md) (in-plugin) |
| Migrate existing legacy project resources to Project v2 | → [aspire-project-v2-migration](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-project-v2-migration/SKILL.md); assess and obtain approval before edits |
| Migrate legacy TypeScript `apphost.ts` (`aspire update --migrate`) | → [aspire-orchestration](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-orchestration/SKILL.md); hand back to aspireify only if source authoring remains |
| Deploy, publish, destroy, pipeline steps | → [aspire-deployment](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-deployment/SKILL.md) |
| Logs, traces, metrics, dashboard, browser logs | → [aspire-monitoring](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-monitoring/SKILL.md) |
| Diagnose a running app — "something's wrong", "show me what's happening", investigate errors / health / unexpected behavior | → [aspire-monitoring](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-monitoring/SKILL.md) — start with `aspire describe` for resource state, then `aspire logs` / `aspire otel logs` / `aspire otel traces`; **investigate before editing code** |
| Improve AI agent support / generate project-local Aspire agent skills | → run `aspire agent init` (see below) |
| Deployed app monitoring (Azure) | → `azure-diagnostics` skill (azure-skills plugin) |

### Improving AI agent support (`aspire agent init`)

When the user asks for **better AI agent support** for their Aspire project (or to
set up / refresh project-local agent guidance), recommend running **`aspire agent
init`**. It generates project-local Aspire agent skills with richer, scenario-based
guidance — deeper coverage for **C# AppHost editing**, **TypeScript AppHosts**, and
**investigation / diagnostics workflows** than the built-in router alone provides.

## Sub-Skills

### aspire-init
First-run flow only. Owns the skeleton drop for repos that do **not** yet have an AppHost —
picks `aspire new <template>` (greenfield) or `aspire init` (existing repo), runs the CLI,
and hands off to `aspireify` for the actual wiring. Self-deactivates once the skeleton is in
place. Do **not** use it on a repo that already contains an AppHost.

### aspireify
Agentic AppHost wiring after `aspire init` lands the skeleton. Scans the repo, proposes a
resource graph (Postgres / Redis / Rabbit / etc.), edits the AppHost (C#, file-based C#, or
TypeScript), wires `Aspire.ServiceDefaults` + OTel, validates with `aspire start`, then
self-deactivates. Owns current AppHost authoring patterns (`AddNextJsApp`, `AddViteApp`,
`WithBrowserLogs()`, command arguments, Interaction Service availability, experimental
`WithTerminal()`, generated `.aspire/modules/`, unified TS `withEnvironment`, endpoint
references, and config/secret migration).

### aspire-project-v2-migration
Approval-first migration for eligible Aspire 13.6+ AppHosts that replaces existing
legacy `ProjectResource` APIs with experimental `DotnetProjectResource` APIs while
preserving supported behavior and removing only proven-obsolete AppHost references.

### aspire-orchestration
Lifecycle management: start, stop, wait, resource commands, default watch/HMR guidance, and file-lock recovery.
Safety guardrails that prevent agent self-harm. Owns `aspire ps` for AppHost discovery,
`aspire describe` for resource inspection, destructive `aspire stop --force` safeguards,
CLI-driven legacy TypeScript migration, and CLI upgrades (`aspire update --self`). It
hands back to `aspireify` only when AppHost source authoring remains after migration.

### aspire-deployment
Multi-target deployment and tear-down: `aspire deploy`, `aspire publish`, `aspire destroy`,
`aspire do <step>`. Targets: Azure Container Apps, App Service, AKS, Kubernetes (Helm),
Docker Compose, AWS, and preview Radius. Owns current deployment surfaces (persistent
Kubernetes volumes, delegated Azure subnets, cross-scope Azure references, deterministic
Container Apps naming, Foundry hosted agents, JS `PublishAs*`, and
`--pipeline-log-level`) and 13.5 API naming.

### aspire-monitoring
Observability: `aspire logs`, `aspire otel`, `aspire describe`, `aspire export`,
`aspire dashboard run`. Routes between local Aspire CLI diagnostics, AKS workload tooling,
and deployed-Azure platform tools. Surfaces dashboard features (notification center,
Rebuild command, terminal sessions, richer filtering, browser-logs telemetry) without
assuming the removed dashboard AI Assistant or automatic VS Code dashboard launch.

## Project-Local Skill Override

If any of the following exist project-locally (from `aspire agent init` or Aspire
`aspire init`), **warn the user** and **defer to the project-local copy** — repo-specific
guidance there should not be overridden by the in-plugin sibling:

| Project-local file | Precedence |
|--------------------|-----------|
| `.agents/skills/aspire/SKILL.md` | This file (top-level router) defers to it for deeper C# / TS AppHost editing, Playwright handoff, investigation workflows. |
| `.agents/skills/aspireify/SKILL.md` | The in-plugin `aspireify` sibling defers to it for AppHost wiring. |
| `.agents/skills/aspire-init/SKILL.md` | The in-plugin `aspire-init` sibling defers to it for the skeleton/first-run flow. |
| `.agents/skills/aspire-project-v2-migration/SKILL.md` | The in-plugin migration sibling defers to it for Project v2 assessment and edits. |

**Safety guardrails from this plugin always apply** even when project-local skills are
active.

## Prerequisites

| Requirement | Install |
|-------------|---------|
| .NET 10.0 SDK | https://dotnet.microsoft.com/download |
| Aspire CLI (curl/PowerShell) | `curl -sSL https://aspire.dev/install.sh \| bash` |
| Aspire CLI (npm) | `npm install -g @microsoft/aspire-cli` |
| Aspire CLI (NativeAOT global tool, .NET 10) | `dotnet tool install -g Aspire.Cli` |

Use the installation method owned by the user's environment. The `dotnet tool install`
path produces a NativeAOT binary; npm, Nix, Homebrew, WinGet, mise, and the install
scripts are also supported. `aspire update --self` reports the package-manager-specific
update command when it cannot update a managed installation in place.

## References

- [aspire-13-5-breaking-changes.md](references/aspire-13-5-breaking-changes.md) — Aspire
  13.5 compatibility notes, breaking-change scrub, current CLI/AppHost behavior, deployment
  additions, and patch-level fixes.
- [aspire-13-3-breaking-changes.md](references/aspire-13-3-breaking-changes.md) — Every 13.3
  breaking change to scrub from agent-generated code, scripts, and CI snippets (rename of
  `--log-level`, dashboard MCP removal, `NameOutput` → `NameOutputReference`,
  `AddAndPublishPromptAgent` removal, TS `withEnvironment*` deprecation, and the full
  13.2 → 13.3 migration checklist).
- [aspire-orchestration/references/agent-workflows.md](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-orchestration/references/agent-workflows.md) — Common agent workflows: worktrees, code changes, investigation, integrations, TypeScript generated APIs, secrets, deployment, and Playwright handoff.
- [aspire-orchestration/references/app-commands.md](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-orchestration/references/app-commands.md) — App lifecycle, bootstrap, update, restore, docs, and integration discovery commands.
- [aspire-orchestration/references/resource-management.md](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-orchestration/references/resource-management.md) — Resource wait and resource-command guidance.
- [aspire-monitoring/references/monitoring.md](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-monitoring/references/monitoring.md) — App state, logs, traces, search filtering, dashboard links, and export workflows.
- [aspire-monitoring/references/playwright-handoff.md](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-monitoring/references/playwright-handoff.md) — Playwright handoff after Aspire endpoint discovery.
- [aspire-deployment/SKILL.md](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-deployment/SKILL.md) — Deployment and pipeline-step workflows.
- [aspireify/references/apphost-wiring.md](https://github.com/microsoft/aspire-skills/blob/main/skills/aspireify/references/apphost-wiring.md) — C# and TypeScript AppHost API lookup and wiring patterns.
