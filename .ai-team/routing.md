# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture analysis | Kaylee | Repo structure, integration patterns, normalization opportunities |
| Code review | Kaylee | Review PRs, check quality, surface inconsistencies |
| Scope & priorities | Kaylee | What to build next, trade-offs, design reviews |
| Contribution ideas | Wash | Missing integrations, backlog items, starter scaffolds |
| Issue drafting | Wash | Write GitHub issue content, propose scoped work |
| Gap analysis | Wash | Weak docs, structural gaps, duplication |
| Documentation | Inara | README improvements, onboarding guides, API docs |
| Onboarding | Inara | New contributor experience, getting-started guides |
| Doc review | Inara | Clarity, consistency, completeness of docs |
| Async issue work (bugs, tests, small features) | @copilot 🤖 | Well-defined tasks matching capability profile |
| Session logging | Scribe | Automatic — never needs routing |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, evaluate @copilot fit, assign `squad:{member}` label | Kaylee |
| `squad:kaylee` | Pick up issue — architecture, code review, structural work | Kaylee |
| `squad:wash` | Pick up issue — contribution scaffolding, issue decomposition | Wash |
| `squad:inara` | Pick up issue — documentation, onboarding, README | Inara |
| `squad:copilot` | Assign to @copilot for autonomous work (if enabled) | @copilot 🤖 |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, **Kaylee** triages it — analyzing content, evaluating @copilot's capability profile, assigning the right `squad:{member}` label, and commenting with triage notes.
2. **@copilot evaluation:** Kaylee checks if the issue matches @copilot's capability profile (🟢 good fit / 🟡 needs review / 🔴 not suitable). If it's a good fit, Kaylee may route to `squad:copilot` instead of a squad member.
3. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
4. When `squad:copilot` is applied and auto-assign is enabled, `@copilot` is assigned on the issue and picks it up autonomously.
5. Members can reassign by removing their label and adding another member's label.
6. The `squad` label is the "inbox" — untriaged issues waiting for Kaylee's review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If architecture is being analyzed, spawn Wash to identify contribution opportunities simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. Kaylee handles all `squad` (base label) triage.
8. **@copilot routing** — when evaluating issues, check @copilot's capability profile in `team.md`. Route 🟢 good-fit tasks to `squad:copilot`. Flag 🟡 needs-review tasks for PR review. Keep 🔴 not-suitable tasks with squad members.
