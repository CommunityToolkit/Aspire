---
name: aspire-init
description: >-
  **WORKFLOW SKILL** - First-run flow for adding Aspire to a repo. Picks `aspire new`
  (greenfield) or `aspire init` (existing repo), drops the AppHost skeleton, then hands
  off to `aspireify` for resource wiring.
  USE FOR: aspire init, aspire new, aspire-starter, aspire-ts-starter, aspire-py-starter,
  add Aspire to existing repo, scaffold Aspire app, bootstrap Aspire, no AppHost detected,
  install aspireify, apphost.mts, generated .aspire/modules.
  DO NOT USE FOR: AppHost wiring on an existing AppHost (use aspireify), start/stop/wait
  (use aspire-orchestration), deploy/publish (use aspire-deployment), logs/traces (use
  aspire-monitoring), repo that already has an AppHost.
  INVOKES: aspire CLI (init, new, doctor), aspireify (handoff after skeleton drop).
  FOR SINGLE OPERATIONS: Run `aspire init` or `aspire new TEMPLATE` directly.
license: MIT
metadata:
  author: Microsoft
  version: "0.0.3"
---

# Aspire Init

> **First-run only.** This skill owns the skeleton drop and template choice for repositories
> that do not yet have an Aspire AppHost. Once the skeleton is in place, hand off to
> [`aspireify`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspireify/SKILL.md) for the actual resource wiring.

## Prerequisites

| Requirement | Install |
|-------------|---------|
| .NET 10.0 SDK | https://dotnet.microsoft.com/download |
| Node.js `^20.19.0`, `^22.13.0`, or `>=24` (TypeScript AppHost) | https://nodejs.org |
| Aspire CLI (curl installer) | `curl -sSL https://aspire.dev/install.sh \| bash` |
| Aspire CLI (npm) | `npm install -g @microsoft/aspire-cli` |
| Aspire CLI (NativeAOT global tool) | `dotnet tool install -g Aspire.Cli` (.NET 10 required) |
| Diagnose missing prerequisites | `aspire doctor` |

> Aspire also supports Nix, Homebrew, WinGet, mise, and the curl/PowerShell installers.
> Use the installation method already owned by the user's environment.

## Detection

Activate **only** when adding Aspire to a workspace that does not yet have one. Confirm ALL
of the following before running `aspire init`:

| Signal | How to Detect | Meaning |
|--------|---------------|---------|
| No C# AppHost | No `.csproj` containing `Aspire.AppHost.Sdk` | OK to init |
| No file-based AppHost | No `apphost.cs` with `#:sdk Aspire.AppHost.Sdk` | OK to init |
| No TypeScript AppHost | No current `apphost.mts` or legacy `apphost.ts` in the repo | OK to init |
| No Aspire config | No `aspire.config.json` in repo root | OK to init |
| User intent | Explicit "add Aspire", "scaffold Aspire", "aspire init" | OK to init |

If **any** AppHost signal is already present, **do not run `aspire init`**. Route to
[`aspireify`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspireify/SKILL.md) (re-wire) or
[`aspire-orchestration`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-orchestration/SKILL.md) (lifecycle).

## Decision: `aspire new` vs `aspire init`

| Situation | Command | Why |
|-----------|---------|-----|
| Empty directory or brand-new project | `aspire new <template>` | Generates a full starter solution |
| Existing repo with services to model | `aspire init` | Drops minimal skeleton + `aspire.config.json` next to existing code |
| User wants a sample to learn from | `aspire new aspire-starter` | Includes ApiService + Web + ServiceDefaults |
| User wants the smallest possible scaffold | `aspire new aspire-empty` (C#) or `aspire new aspire-ts-empty` (TS) | No resources pre-wired |
| User wants Python services | `aspire new aspire-py-starter` (TypeScript AppHost drives Python) | **Not** `dotnet new` — that template was removed in 13.3 |

See [references/templates.md](references/templates.md) for the complete template list and
options.

## Workflow A — `aspire new <template>` (new project)

For brand-new projects in an empty or non-existent directory:

1. Confirm prerequisites with `aspire doctor` if the CLI install is uncertain.
2. Pick a template from [references/templates.md](references/templates.md).
3. Run the template, append `--non-interactive` for agent flows:
   ```bash
   aspire new aspire-starter --name MyApp --output ./MyApp --non-interactive
   ```
4. The new directory is fully wired by the template — **no aspireify handoff needed**.
5. Route to [`aspire-orchestration`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-orchestration/SKILL.md) for first run
   (`aspire start`).

## Workflow B — `aspire init` (existing repo)

For repositories that already contain services (Express API, .NET API, Python service, etc.)
and need an AppHost added alongside them:

1. Verify the [Detection](#detection) table — confirm **no** AppHost is present.
2. Run `aspire init`, choosing language explicitly for non-interactive flows:
   ```bash
   aspire init --language csharp --non-interactive
   # or
   aspire init --language typescript --non-interactive
   ```
3. `aspire init` drops:
   - The AppHost skeleton (`apphost.cs` with `#:sdk` directives, **or** `apphost.mts` with the
     generated `.aspire/modules/` folder)
   - AppHost configuration describing language + AppHost path
   - The **`aspireify`** agent skill into the project's skill directory (same one
     `aspire agent init` uses)
4. **Hand off to `aspireify`** — `aspire init` does **not** wire resources, projects, or
   integrations on its own.
5. After `aspireify` finishes wiring, validate via `aspire start`
   ([`aspire-orchestration`](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire-orchestration/SKILL.md)).

In 13.5, a TypeScript init inside a repo that already has a root `package.json` creates a
nested `aspire-apphost/` package and points the root `aspire.config.json` at
`aspire-apphost/apphost.mts`. A solution-backed C# repo can receive a project-based AppHost;
new C# AppHosts enable `AspireUseCliBundle=true` by default. Preserve these generated choices.

See [references/init-workflow.md](references/init-workflow.md) for the full sequence
including what `aspire.config.json` contains and what to do if `aspire init` fails partway.

## Handoff Rules

| After `aspire init` / `aspire new` finishes... | Route To |
|------------------------------------------------|----------|
| Skeleton dropped, resources need wiring | → `aspireify` skill (in-plugin or project-local) |
| Skeleton dropped, validate it starts | → `aspire-orchestration` (run `aspire start`) |
| New project from template, ready to run | → `aspire-orchestration` |
| User asks to deploy after init | → `aspire-deployment` |
| User asks for logs/traces after init | → `aspire-monitoring` |
| Existing AppHost detected — do NOT run init | → `aspireify` (re-wire) or `aspire-orchestration` (lifecycle) |

## Project-Local Skill Override

If `.agents/skills/aspire-init/SKILL.md` exists project-locally (legacy install from an
older `aspire init` run), **warn the user and defer to it**. The legacy project-local skill
may carry repo-specific guidance that should not be overridden by this in-plugin skill.

The project-local `aspireify` skill (installed by `aspire init`) takes precedence
over this plugin's in-plugin `aspireify` for the same reason — defer to the project-local
copy and warn.

## Error Handling

| Symptom | Cause | Action |
|---------|-------|--------|
| `aspire init` reports AppHost already exists | Repo already has an AppHost | Stop. Route to `aspireify` (re-wire) or `aspire-orchestration` (lifecycle) |
| `aspire init` fails in non-interactive mode without `--language` | Multiple language paths available | Re-run with `--language csharp` or `--language typescript` |
| `aspire new` rejects `--output` path | Path exists and is non-empty | Use a different `--output` or empty the directory |
| `aspire` command not found | CLI not installed | `dotnet tool install -g Aspire.Cli` (.NET 10) or `curl -sSL https://aspire.dev/install.sh \| bash` |
| `aspire doctor` reports missing .NET 10 | SDK missing | Install .NET 10 SDK before retrying |
| `aspire init` succeeded but no `aspireify` skill installed | Agent skill directory not detected | Run `aspire agent init` to install `aspireify`, then continue wiring |
| Skeleton dropped but resources not wired | Expected — `aspire init` does not wire | Hand off to `aspireify` |
| Existing TypeScript AppHost still uses `apphost.ts` | Legacy entry point and package graph | Hand off to `aspire-orchestration`, which owns approval and `aspire update --migrate --yes --non-interactive`; return to aspireify only for later source authoring |

## References

- [templates.md](references/templates.md) — `aspire new` templates and options
- [init-workflow.md](references/init-workflow.md) — `aspire init` flow, `aspire.config.json`
  layout, and `aspireify` handoff
- [aspire-13-5-breaking-changes.md](https://github.com/microsoft/aspire-skills/blob/main/skills/aspire/references/aspire-13-5-breaking-changes.md) —
  13.5 migrations, CLI-bundle behavior, and fixes for affected releases
