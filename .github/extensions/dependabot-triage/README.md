# Dependabot triage canvas

A Copilot app canvas for triaging open Dependabot PRs in this repository.

- Lists **open** Dependabot PRs with check status, mergeability and review state, grouped by dependency, directory, ecosystem or status.
- Matches open Dependabot **security alerts** to the PRs that fix them, and flags alerts that no open PR fixes.
- Shows **overlaps**: the same dependency bumped in many PRs, and PRs that touch the same files (lockfiles, `Directory.Packages.props`) and will conflict with each other.
- Shows the PR's **diff** in the detail view. Manifest changes open automatically; lockfiles and very large files start collapsed. The agent can read the same diff with the `get_pr_diff` action, which summarises lockfiles unless it asks for them.
- From the canvas you can **approve**, **approve & merge** (optionally with auto-merge), **close**, and post supported `@dependabot` commands (`rebase`, `recreate`, `ignore …`), one PR at a time or in bulk.
- **Review with agent** sends one or more PRs to an agent for review. The agent records its verdict with the `record_review` action and the verdict appears in the canvas. It is marked stale if the PR's head changes.
- **Combine into one PR** (on a group header, the bulk bar, a PR's "Combine all", or an Overlaps row) asks an agent to fold several Dependabot PRs into a single PR. You choose the title and branch, whether to open it as a draft, and whether the agent may fix breakages. The agent works in a separate `git worktree` so your checkout is untouched. It cherry-picks each PR, regenerates conflicting lockfiles, pushes, opens the PR with the union of labels from the included PRs plus `dependencies` and a `Supersedes #n` list, and reports back with the `record_combined_pr` action. Superseded PRs get a `⊕#N` badge, and the Overlaps tab lists combined PRs. `Supersedes #n` isn't a closing keyword (and closing keywords don't close PRs anyway), so merging the combined PR leaves the originals open until Dependabot's next scheduled run for that ecosystem notices they're up to date. When a combined PR is merged and its superseded PRs are still open, the canvas shows a banner with a **Close N superseded…** button.

- **Background agents.** Reviews and combines run in a new background agent by default, so several can proceed at once while the main session stays free. The **Run in** picker in each dialog can also send the work as a follow-up to an agent that is still running or idle, or queue it in the current session. The **Agents** tab lists every agent started from the canvas with its status, current step, PRs and latest response, and lets you message, cancel or dismiss it. PRs an agent is working on show a ⚙ badge. Background agents report back by calling the canvas action, or by ending their reply with a fenced dependabot-canvas JSON block that the canvas reads and applies. A review is only recorded for PRs sent to that agent, and a combine result only for that agent's own request. If the runtime can't start a background agent, the work goes to the current session instead and the canvas tells you. Known limitation: when a follow-up to an existing agent finishes, the runtime only reports it once the main session is between turns, so its status can lag while the main session is busy. Results the agent records directly through the canvas action are not affected.

Open it by asking Copilot to "open the Dependabot triage canvas", or call `open_canvas` with `canvasId: "dependabot-triage"`. Pass `{ "repo": "owner/name" }` to target a different repository.

## Requirements

- The [GitHub CLI](https://cli.github.com/) (`gh`), authenticated with `gh auth login`. Every GitHub call goes through `gh`.
- Write access to the repository if you want to approve, merge, close or comment. Read access is enough for triage.
- Security alert read access to see Dependabot alerts. Without it the alerts view shows a warning and the rest keeps working.

## Safety

- Approve, merge, close and comment only happen when a person clicks through a confirmation dialog in the canvas. The agent-facing actions can read, filter, select and record results, but they cannot mutate PRs. A combine request is started by the user from the canvas. The agent is told to push only its own new branch and PR, and never to approve, merge, close or comment on the originals.
- Merges use `--match-head-commit`, so a PR that changed after you looked at it won't be merged.
- Bulk mutations run one at a time with spacing, to avoid GitHub secondary rate limits.
- The canvas server listens on loopback only and needs a per-instance token.

Agent review verdicts (`reviews.json`) and combine requests (`combined.json`) are stored per user under `$COPILOT_HOME/extensions/dependabot-triage/artifacts/`, not in the repository. A combine record is removed automatically once its combined PR is closed or merged and none of the PRs it replaces are still open. An unfinished request is removed after 7 days. Background agent IDs belong to the Copilot session, so the Agents list is stored in the session workspace (`files/dependabot-triage-agents.json`).
