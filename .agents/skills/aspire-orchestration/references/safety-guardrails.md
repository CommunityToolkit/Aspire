# Safety Guardrails — Aspire Agent Rules

> **Purpose**: Detailed explanation of why each guardrail exists, what goes wrong when violated, and how to recover.

## Why These Rules Exist

Issue [#15801](https://github.com/microsoft/aspire/issues/15801) documented 5 specific failures when AI agents work in Aspire projects without guidance. Every rule below directly prevents one or more of these failures.

---

## Rule 1: Use Aspire Lifecycle Routing, Not `dotnet run`, For Agent Starts

### Why explicit Aspire lifecycle is required for agents

New 13.5 C# templates enable `AspireUseCliBundle=true`, so `dotnet run` can resolve and
delegate to the Aspire CLI. Existing AppHosts remain opt-in, and even a bundled
`dotnet run` is the wrong agent lifecycle entry point because it:

- runs in the foreground instead of returning control to the agent;
- cannot express the skill's exact-target, `--non-interactive`, and worktree
  `--isolated` contract;
- may bypass Aspire entirely in an older or opted-out AppHost; and
- does not participate in editor-owned lifecycle tracking.

### Correct Pattern

In VS Code, prefer `aspire_apphost_start` for a discovered AppHost. Load the tool first
when it is deferred. Use mode `run` with the exact selected `appHostPath` unless the user
explicitly asks to attach a debugger.

In a multi-root workspace, that tool-only `appHostPath` may look like
`repo-a~1/MyApp.AppHost/MyApp.AppHost.csproj`. The CLI `--apphost` flag does not
understand that selector namespace. Before any CLI fallback, resolve the same selected
AppHost to its actual filesystem project path.

| Use | Example |
|-----|---------|
| Tool-only selector | `repo-a~1/MyApp.AppHost/MyApp.AppHost.csproj` |
| CLI fallback path (POSIX) | `/workspaces/repo-a/MyApp.AppHost/MyApp.AppHost.csproj` |
| CLI fallback path (Windows) | `C:\workspaces\repo-a\MyApp.AppHost\MyApp.AppHost.csproj` |

That CLI project path may be workspace-relative (`MyApp.AppHost/MyApp.AppHost.csproj`)
or absolute; use the current platform's native path syntax. If the selected
`appHostPath` is already a normal workspace-relative project path such as
`MyApp.AppHost/MyApp.AppHost.csproj`, reuse it unchanged for CLI fallbacks.

The exception is a git worktree: the editor tool cannot currently request isolated Aspire
state, so use the resolved filesystem path with the CLI.

```bash
# ✅ Worktree fallback
aspire start --non-interactive --isolated --apphost <filesystem-path>

# ✅ Editor-tool-unavailable fallback outside a worktree
aspire start --non-interactive --apphost <filesystem-path>

# ✅ Verify it's running
aspire ps
```

### `aspire run` vs `aspire start`

| Command | Mode | Dashboard | Use Case |
|---------|------|-----------|----------|
| `aspire run` | Foreground (interactive) | Yes, in terminal | Human developer at terminal |
| `aspire start` | Background (detached) | No terminal output | AI agent CLI fallback |
| `aspire run --detach` | Background (same as start) | Yes, separate window | Alternative to `aspire start` |

For AI agents outside the editor lifecycle, use
`aspire start --non-interactive --apphost <filesystem-path>` so it runs in the background and
returns control to the agent.

### Recovery If `dotnet run` Was Used

Restart through lifecycle routing: prefer `aspire_apphost_start` with mode `run` and the
exact selected `appHostPath`. Use the CLI only when the editor tool is unavailable, or
when a git worktree requires `--isolated`.

```bash
# Stop the foreground run cleanly, then use the editor lifecycle tool.
# If the editor tool is unavailable:
aspire start --non-interactive --apphost <filesystem-path>
# In a git worktree:
aspire start --non-interactive --isolated --apphost <filesystem-path>
```

---

## Rule 2: ALWAYS `aspire wait` — NEVER `curl` Polling

### Why `curl` Polling Is Wrong

Aspire tracks resource readiness internally through health checks, dependency graphs, and startup ordering. Manual HTTP polling:

- **Doesn't know the correct port** — Aspire assigns ports dynamically
- **Doesn't respect dependency ordering** — a resource may respond to HTTP before its dependencies are ready
- **Creates false negatives** — health endpoint may not exist or may return 503 during startup
- **Wastes time** — retry loops with arbitrary delays vs. event-driven readiness

### Correct Pattern

```bash
# ✅ Wait for a specific resource
aspire wait apiservice

# ✅ Wait with extended timeout for slow resources
aspire wait postgres --timeout 120

# ✅ Wait then interact
aspire wait apiservice && curl $(aspire describe apiservice --format Json | jq -r '.endpoints[0].url')
```

### Machine-Readable Output

Always use `--format Json` when parsing output programmatically:

```bash
# ✅ Get resource endpoints as JSON
aspire describe --format Json

# ❌ Never scrape text output
aspire describe | grep "http"  # fragile, format may change
```

---

## Rule 3: Prefer Resource Commands / Watch — NEVER `dotnet build` With Aspire Running

### Why `dotnet build` Causes File-Lock Errors

When Aspire is running, it holds file locks on:

- Output assemblies (`.dll`, `.pdb`) of managed resources
- Build intermediates in `obj/` directories
- NuGet package caches being read by running processes

Running `dotnet build` while Aspire holds these locks produces:

```
error MSB3491: Could not write to output file 'bin/Debug/net10.0/MyService.dll'
error CS2012: Cannot open 'obj/Debug/net10.0/MyService.dll' for writing
```

The agent then concludes the project is "un-buildable" — a false conclusion.

### Recovery If `dotnet build` Was Used and Locks Hit

When you see `MSB3491` / `CS2012` / "file in use" / "another process is using":

Resolve the exact AppHost, then stop and restart it through the lifecycle routing in
Rule 4. If that routing permits a CLI fallback, resolve the selected AppHost to its
filesystem path and keep both commands scoped to the same AppHost:

```bash
# ✅ CLI stop only after Rule 4 permits fallback
aspire stop --non-interactive --apphost <filesystem-path>

# After rebuilding, restart only when needed and the editor tool is unavailable
aspire start --non-interactive --apphost <filesystem-path>

# Worktree restart
aspire start --non-interactive --isolated --apphost <filesystem-path>
```

> 🔒 **Stopping the AppHost is the ONLY first step.** Prefer `aspire_apphost_stop` when
> VS Code exposes it. Do not `pkill dotnet`, do not delete
> `bin/`/`obj/`, do not "reboot to release the lock", and do not tell the user the
> project has a permanent build failure. The cause is always the same — Aspire is
> holding the output files — and the fix is always to stop it cleanly.

| ❌ NEVER (file-lock recovery) | ✅ ALWAYS |
|------------------------------|----------|
| Say the project has a permanent build failure when you see `MSB3491`/`CS2012` | Recognize Aspire is holding locks and stop the AppHost through lifecycle routing |
| `pkill dotnet` / `kill <PID>` | Editor stop tool or exact-target CLI fallback |
| `rm -rf bin obj` to "force" the build | Stop the AppHost, then rebuild |
| Suggest a reboot | Stop the AppHost through lifecycle routing |
| Re-run `dotnet build` with Aspire still up | Stop the AppHost first; prefer resource commands/watch/HMR/debug workflow |

### What Changed Determines the Action

| What Changed | Action | Command |
|--------------|--------|---------|
| AppHost project (Program.cs, .csproj) | Full restart | Stop and restart through lifecycle routing |
| .NET service project (.cs files) | Rebuild/refresh resource if exposed | `aspire resource <name> rebuild` or the resource's IDE/watch workflow |
| JavaScript/Python/Go files | Usually no Aspire action | File watchers/HMR handle it automatically |
| Configuration (appsettings.json) | Check first | `aspire describe` then decide |

---

## Rule 4: Use Aspire Lifecycle Routing For Cleanup — NEVER Leave Unwanted Processes Running

### Why Cleanup Matters

Aspire orchestrates multiple processes (your services, databases, message brokers, etc.). Leaving them running causes:

- **Port conflicts** — next `aspire start` fails because ports are occupied
- **File locks** — can't build or modify service code
- **Resource consumption** — databases, Redis, etc. consuming memory
- **Stale state** — old code running while you've made changes
- **Orphaned containers** — Docker containers left running

### Correct Pattern

If VS Code exposes `aspire_apphost_stop`, load and call it first with the exact selected
`appHostPath`. `run` mode is still editor-owned even though no debugger is attached.
If the stop tool is unavailable, resolve the selected AppHost to its filesystem path and
use `aspire stop --non-interactive --apphost <filesystem-path>`.

| Tool result | Required action |
|-------------|-----------------|
| `stopped` or `notRunning` | Report the result and take no further stop action |
| `alreadyStopping`, controller `editor` | Report that the editor stop is already in progress and take no further stop action |
| `alreadyStarting`, controller `editor` | Retry `aspire_apphost_stop` once; if it repeats, report that startup is still in progress and do not use the CLI |
| `notEditorOwned`, controller `external` | Resolve the selected AppHost to its filesystem path, then use `aspire stop --non-interactive --apphost <filesystem-path>` when the user requested that target be stopped |
| `failed`, controller `unknown` | Retry the tool once; use the same exact-target fallback only if that result repeats |
| `ambiguousSession` | Stop nothing and have the user disambiguate in the editor; never run or offer a CLI fallback, even with confirmation |
| Any other refusal or failure | Resolve or report it without changing mechanisms |
| Unclear target among multiple AppHosts | Ask which one and take no lifecycle action |

These rows are mutually exclusive. Act only on the current result and do not offer a
command from another row as a speculative future workaround. When a CLI fallback is
allowed, keep the target exact by reusing the same selected AppHost after resolving it to
the CLI filesystem path.

```bash
# ✅ If the stop tool is unavailable, or after notEditorOwned/external,
# ✅ or a repeated failed/unknown result
aspire stop --non-interactive --apphost <filesystem-path>

# ✅ Verify everything stopped
aspire ps  # should show no running AppHost for that path
```

### Recovery from Orphaned Processes

```bash
# Check if anything is still running
aspire ps

# If aspire ps shows nothing but ports are blocked:
# The previous instance may have crashed. Start fresh:
aspire start --non-interactive --apphost <filesystem-path>  # editor-tool-unavailable fallback
```

### Destructive persistent-resource cleanup

`aspire stop --force` is not a stronger ordinary stop. It stops one selected AppHost and
then permanently removes its persistent resource instances without another confirmation.
Require explicit data-loss approval and an exact `--apphost` target. Never combine
`--force` with `--all`.

---

## Rule 5: ALWAYS `--format Json` for Machine-Readable Output

### Why JSON Output Matters for Agents

Text output is formatted for humans and may change between versions. JSON output is:

- **Stable** — structured contract unlikely to break
- **Parseable** — `jq`, Python, or any JSON parser works
- **Complete** — includes fields not shown in text output

### Examples

```bash
# ✅ Machine-readable AppHost list
aspire ps --format Json

# ✅ Get specific resource details
aspire describe apiservice --format Json

# ✅ Parse with jq
aspire describe --format Json | jq '.resources[] | select(.state == "Running")'
```

### ⚠️ Known JSON Output Issues

| Issue | Workaround |
|-------|-----------|
| `aspire start --format json` may emit human-readable text before JSON ([#15843](https://github.com/microsoft/aspire/issues/15843)) | Strip non-JSON lines before parsing |
| `aspire stop` does NOT support `--format json` yet | Use exit code for success/failure |

### Hidden Resources and `--include-hidden`

`aspire ps` is AppHost-level in 13.5 and does not list resources. `aspire describe`
filters resources marked hidden in the AppHost (proxies, helper containers, migration jobs).
This filtering is correct for normal workflows — agents and humans see only the resources they
care about, not the implementation scaffolding.

Use `--include-hidden` when:

| Situation | Why |
|-----------|-----|
| Debugging a proxy or sidecar | Proxies are hidden by default; you need their state to diagnose connectivity |
| Investigating helper containers | Helper containers (e.g. wait-for-it shims, init containers) are hidden |
| Tracking down migration jobs | Migration / seed jobs are typically hidden once they finish |
| Expected resources are missing from `aspire describe` | The resource may exist but be marked hidden — confirm with `--include-hidden` before assuming the AppHost is wrong |
| Parsing for completeness in agent automation | A full-graph view requires explicit opt-in |

```bash
# ✅ AppHost discovery
aspire ps --format Json

# ✅ Resource debugging / completeness — include hidden resources
aspire describe --include-hidden --format Json
```

If a user reports "I can't see my proxy / migration / helper container," reach for
`aspire describe --include-hidden` before assuming the AppHost is misconfigured. On
`aspire resource`, `--include-hidden` exposes hidden commands rather than hidden resources.

---

## Rule 6: ALWAYS `--non-interactive` for Agent Commands

### Why It Matters

AI agents run in non-interactive terminals. Some Aspire CLI commands may prompt for confirmation or input. Always pass `--non-interactive` to prevent hangs:

```bash
# ✅ Agent-safe commands
aspire start --non-interactive --apphost <filesystem-path>
aspire deploy --non-interactive
aspire agent init --non-interactive
```

> ⚠️ **Known issue**: `aspire agent init --non-interactive` is broken in some versions ([#16264](https://github.com/microsoft/aspire/issues/16264), [#15071](https://github.com/microsoft/aspire/issues/15071)). If it fails, instruct the user to run it from a standard terminal.

---

## Recovery Patterns Summary

| Mistake Made | Recovery Steps |
|-------------|---------------|
| Used `dotnet run` on AppHost | Stop the foreground run cleanly, then start through lifecycle routing |
| Used `dotnet build` and got file locks | Stop through lifecycle routing, wait 2s, then `dotnet build` or restart |
| Used `curl` polling and got false results | `aspire wait <resource>`, then use endpoints from `aspire describe` |
| Left Aspire running, now ports conflict | Stop and restart through lifecycle routing |
| Resource won't start after code change | Fix code, then use resource commands/watch/HMR/debug workflow or restart the AppHost if the AppHost model changed |
| Nothing works, environment broken | `aspire doctor` to diagnose, then follow recommendations |
