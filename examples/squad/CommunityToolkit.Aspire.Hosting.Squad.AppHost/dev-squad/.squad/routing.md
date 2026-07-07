# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture & design | Lisa | Module boundaries, system design, technology selection, design proposals |
| Scope & priorities | Lisa | What to build next, trade-offs, scope cuts, prioritization calls |
| Cross-cutting decisions | Lisa | Auth model, error handling, logging strategy, persistence approach |
| Code review (non-trivial) | Lisa | Public-API changes, shared-module edits, architectural deltas |
| Frontend / UI | Marge | Components, pages, layouts, styling, design-system contributions |
| Accessibility | Marge | Keyboard nav, ARIA, contrast, screen-reader behavior, semantic HTML |
| UX polish & copy | Marge | Empty states, loading states, error states, microcopy, tone |
| Frontend state | Marge | Client state shape, data flow, form handling |
| Backend / APIs | Frink | HTTP/RPC endpoints, contracts, versioning, request/response shape |
| Data modeling | Frink | Schema design, migrations, indexes, transactions, constraints |
| Services & integrations | Frink | Background jobs, queues, third-party integrations, scheduling |
| Performance & observability | Frink | Profiling, indexing, caching, logs/metrics/traces, health checks |
| Test strategy & authoring | Comic Book Guy | Unit / integration / e2e tests, regression suites, test pyramid calls |
| Edge cases & bug reports | Comic Book Guy | Boundary analysis, exploratory testing, reproducible repros |
| Test review / coverage gating | Comic Book Guy | Rejects features shipped without tests; reviews test quality |
| Session logging | Scribe | Automatic — never needs routing |
| Work queue / backlog watching | Ralph | "Ralph, go", "keep working", backlog drain, idle-watch |
| RAI review | Rai | Content safety, bias checks, credential detection, ethical review |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Lead |
| `squad:{name}` | Pick up issue and complete the work | Named member |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
