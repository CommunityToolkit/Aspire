// Extension: dependabot-triage
// Canvas for triaging open Dependabot PRs: checks, mergeability, related
// security alerts and overlaps, with sign-off/merge, Dependabot commands and
// hand-off to the current Copilot session for deeper review.

import { joinSession, createCanvas, CanvasError } from "@github/copilot-sdk/extension";
import { detectRepo } from "./github.mjs";
import { startInstanceServer } from "./server.mjs";
import { getStore } from "./store.mjs";
import { formatDiff } from "./model.mjs";
import { AgentResultValidationError, AgentTracker } from "./agents.mjs";

const CANVAS_ID = "dependabot-triage";
const AUTO_REFRESH_MS = 5 * 60_000;
const REPO_PATTERN = "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$";

const instances = new Map(); // instanceId -> { server, store, uiState }
let detectedRepo = null;
let session;
const agents = new AgentTracker();
agents.setResultHandler(({ agent, action, input }) => applyAgentResult(agent, action, input));

/**
 * Sends work to a background agent (new, or an existing one as a follow-up) or
 * to the main session. Falls back to the main session if the runtime can't
 * start background agents.
 */
async function dispatch({ store, target = "new", kind, numbers, requestId, name, description, buildPrompt }) {
    if (target === "main") {
        await session.send({ prompt: buildPrompt({ background: false }) });
        return { target: "main" };
    }
    const prompt = buildPrompt({ background: true });
    if (target !== "new") {
        if (!agents.get(target)) throw Object.assign(new Error("That agent is no longer tracked by the canvas."), { status: 404 });
        const rec = await agents.message(target, { prompt, kind, numbers, requestId });
        return { target: "agent", agentId: rec.agentId, name: rec.name, created: false };
    }
    try {
        const rec = await agents.start({ repo: store.repo, kind, name, description, prompt, numbers, requestId });
        return { target: "agent", agentId: rec.agentId, name: rec.name, created: true };
    } catch (e) {
        await session.send({ prompt: buildPrompt({ background: false }) });
        return { target: "main", fallback: e.message.split("\n")[0] };
    }
}

async function applyAgentResult(agent, action, input) {
    const store = getStore(agent.repo);
    const ints = (v) => (Array.isArray(v) ? v.map(Number).filter((n) => Number.isInteger(n) && n > 0) : []);
    if (action === "record_review") {
        const numbers = ints(input.numbers).filter((n) => agent.numbers.includes(n));
        if (!numbers.length || !["safe", "caution", "blocked"].includes(input.verdict)) throw new AgentResultValidationError("invalid record_review payload");
        await store.recordReview(numbers, { verdict: input.verdict, summary: String(input.summary ?? "").slice(0, 4000), reviewer: `agent: ${agent.name}` });
    } else if (action === "record_combined_pr") {
        const requestId = typeof input.requestId === "string" && agent.requestIds.includes(input.requestId) ? input.requestId : null;
        if (!requestId) throw new AgentResultValidationError("record_combined_pr payload has no requestId from this agent");
        const prNumber = Number.isInteger(input.prNumber) && input.prNumber > 0 ? input.prNumber : undefined;
        const excluded = Array.isArray(input.excluded)
            ? input.excluded.map((e) => (typeof e === "number" ? { number: e } : e)).filter((e) => Number.isInteger(e?.number)).map((e) => ({ number: e.number, reason: String(e.reason ?? "").slice(0, 500) }))
            : [];
        try {
            await store.recordCombinedPr({ requestId, prNumber, included: ints(input.included), excluded, summary: input.summary ? String(input.summary).slice(0, 4000) : undefined });
        } catch (e) {
            if (e.message?.startsWith("Unknown combine request ")) throw new AgentResultValidationError(e.message);
            throw e;
        }
    } else {
        throw new AgentResultValidationError(`unknown action ${action}`);
    }
}

async function resolveRepo(input) {
    if (input?.repo) return input.repo;
    if (!detectedRepo) {
        try {
            detectedRepo = await detectRepo(process.cwd());
        } catch (e) {
            throw new CanvasError("repo_not_detected", `Could not detect the GitHub repository from ${process.cwd()}; pass { repo: "owner/name" }. ${e.message}`);
        }
    }
    return detectedRepo;
}

function instanceFor(ctx) {
    const entry = instances.get(ctx.instanceId);
    if (!entry) throw new CanvasError("instance_not_found", `Canvas instance ${ctx.instanceId} is not open.`);
    return entry;
}

function describeUpdates(pr) {
    if (!pr.updates.length) return pr.title;
    return pr.updates
        .map((u) => `${u.name} ${u.from ?? "?"} → ${u.to ?? "?"} (${u.type}${u.dependencyType ? `, ${u.dependencyType}` : ""})`)
        .join("; ");
}

function compactPr(pr) {
    return {
        number: pr.number,
        url: pr.url,
        ecosystem: pr.ecosystem,
        directory: pr.directory,
        updates: describeUpdates(pr),
        updateType: pr.highestUpdateType,
        status: pr.status,
        checks: pr.checks,
        mergeable: pr.mergeable,
        mergeStateStatus: pr.mergeStateStatus,
        reviewDecision: pr.reviewDecision,
        autoMerge: !!pr.autoMerge,
        fixesAlerts: pr.security.fixes,
        sameDependencyPrs: pr.overlaps.sameDependency.length,
        sharedFilePrs: pr.overlaps.sharedFiles.length,
        agentReview: pr.agentReview ? { verdict: pr.agentReview.verdict, stale: pr.agentReview.stale } : null,
    };
}

const PARALLEL_NOTE =
    "You are running as a background agent started from the Dependabot triage canvas; other agents may be working in this repository at the same time. Do not modify, switch branches in, or stash the main checkout — if you need to build or edit anything, use your own uniquely named git worktree outside the repo folder and remove it when done.";

function recordInstruction(instanceId, action, inputTemplate, { background }) {
    const call = `invoke_canvas_action({ instanceId: "${instanceId}", actionName: "${action}", input: ${inputTemplate} })`;
    if (!background) return `When finished, record the outcome in the canvas with ${call}.`;
    return [
        `When finished (or if you give up), record the outcome in the canvas: call ${call} if that tool is available to you, and in any case end your final reply with a fenced block like the one below (the canvas reads it from your reply), filling in real values as JSON:`,
        "",
        "```dependabot-canvas",
        `{ "action": "${action}", "input": ${inputTemplate} }`,
        "```",
    ].join("\n");
}

function buildReviewPrompt(store, instanceId, numbers, note, { background = false } = {}) {
    const snap = store.snapshot();
    const alertsByNumber = new Map(snap.alerts.map((a) => [a.number, a]));
    const prs = numbers.map((n) => snap.prs.find((p) => p.number === n)).filter(Boolean);
    const lines = [
        `Please review ${prs.length === 1 ? "this Dependabot pull request" : `these ${prs.length} related Dependabot pull requests`} in ${store.repo} (sent from the Dependabot triage canvas).`,
        "",
    ];
    for (const pr of prs) {
        lines.push(`### #${pr.number} ${pr.title}`);
        lines.push(`- URL: ${pr.url}`);
        lines.push(`- Ecosystem/directory: ${pr.ecosystem} ${pr.directory}`);
        lines.push(`- Updates: ${describeUpdates(pr)}`);
        lines.push(`- Checks: ${pr.checks ?? "none"}; mergeable: ${pr.mergeable} (${pr.mergeStateStatus}); review: ${pr.reviewDecision ?? "n/a"}`);
        const details = store.details.get(pr.number);
        const failing = details?.checks?.filter((c) => ["FAILURE", "ERROR", "TIMED_OUT", "CANCELLED", "ACTION_REQUIRED"].includes(c.state)) ?? [];
        if (failing.length) lines.push(`- Failing checks: ${failing.map((c) => `${c.workflow ? `${c.workflow} / ` : ""}${c.name} (${c.url ?? "no link"})`).join(", ")}`);
        const fixes = pr.security.fixes.map((n) => alertsByNumber.get(n)).filter(Boolean);
        if (fixes.length) lines.push(`- Fixes security alerts: ${fixes.map((a) => `${a.ghsa ?? `#${a.number}`} (${a.severity}) ${a.summary}`).join("; ")}`);
        if (pr.overlaps.sameDependency.length) lines.push(`- Same dependency also updated in: ${pr.overlaps.sameDependency.map((n) => `#${n}`).join(", ")}`);
        if (pr.overlaps.sharedFiles.length) lines.push(`- Shares files with: ${pr.overlaps.sharedFiles.slice(0, 15).map((o) => `#${o.number}`).join(", ")}${pr.overlaps.sharedFiles.length > 15 ? " …" : ""}`);
        lines.push("");
    }
    lines.push(
        "Focus on: breaking changes in the release notes/changelog between the from/to versions, whether code in this repo needs to change, why any checks are failing, and whether it is safe to merge (and in what order, given overlaps).",
        "Use `gh pr view`/`gh pr diff`/`gh pr checks` and the local checkout as needed (or the canvas's `get_pr_diff` action, which summarises lockfiles). Do not approve, merge or comment on the PRs yourself — the user signs off from the canvas.",
    );
    if (background) lines.push("", PARALLEL_NOTE);
    lines.push(
        "",
        recordInstruction(instanceId, "record_review", `{ "numbers": [${numbers.join(", ")}], "verdict": "safe" | "caution" | "blocked", "summary": "<one or two sentences>" }`, { background }),
    );
    if (note) lines.push("", `Additional instructions from the user: ${note}`);
    return lines.join("\n");
}

function buildCombinePrompt(store, instanceId, request, note, { background = false } = {}) {
    const snap = store.snapshot();
    const prs = request.numbers.map((n) => snap.prs.find((p) => p.number === n)).filter(Boolean);
    const base = request.base;
    const alertsByNumber = new Map(snap.alerts.map((a) => [a.number, a]));
    const fixes = [...new Set(prs.flatMap((p) => p.security.fixes))].map((n) => alertsByNumber.get(n)).filter(Boolean);
    const lines = [
        `Please combine these ${prs.length} open Dependabot pull requests in ${store.repo} into a single pull request that replaces them (requested from the Dependabot triage canvas, request id \`${request.id}\`).`,
        "",
        `- New branch: \`${request.branch}\` (from \`origin/${base}\`)`,
        `- PR title: ${request.title}`,
        `- Open as draft: ${request.draft ? "yes" : "no"}`,
        `- Fix code/build breaks caused by the updates: ${request.fixBreakages ? "yes — make the minimal changes needed and call them out in the PR body" : "no — only apply the dependency updates; list anything that breaks in the PR body instead of fixing it"}`,
        "",
        "## Pull requests",
        "",
        "| PR | Branch | Head | Directory | Updates | Checks | Files |",
        "|---|---|---|---|---|---|---|",
    ];
    for (const pr of prs) {
        const files = pr.files.slice(0, 6).join(", ") + (pr.files.length > 6 || pr.filesTruncated ? ", …" : "");
        lines.push(`| #${pr.number} | \`${pr.headRefName}\` | \`${pr.headOid?.slice(0, 12) ?? "?"}\` | ${pr.ecosystem} \`${pr.directory}\` | ${describeUpdates(pr).replace(/\|/g, "\\|")} | ${pr.checks ?? "none"}${pr.mergeable === "CONFLICTING" ? ", conflicts" : ""} | ${files} |`);
    }
    if (fixes.length) {
        lines.push("", `Security alerts fixed by these PRs: ${fixes.map((a) => `${a.ghsa ?? `#${a.number}`} (${a.severity}, ${a.package})`).join("; ")}. Make sure the combined PR still resolves them.`);
    }
    lines.push(
        "",
        "## How to do it",
        "",
        "1. The current checkout may have unrelated uncommitted work — do **not** switch branches in it or stash anything. Instead `git fetch origin` and create a separate worktree outside the repo folder, e.g. `git worktree add -b " +
                    request.branch + ` <tmp>/${request.id} origin/${base}\`, and do all the work there. Use \`refs/${request.id}/<n>\` for fetched PR heads so parallel combines don't collide.`,
        "2. For each PR, fetch its head (`git fetch origin pull/<n>/head`) and apply its changes onto the new branch, e.g. `git cherry-pick $(git merge-base origin/" + base + " <headOid>)..<headOid>`. Dependabot PRs are usually a single commit.",
        "3. If a cherry-pick conflicts on a lockfile (package-lock.json, yarn.lock, pnpm-lock.yaml, packages.lock.json, …), keep the base branch version of the lockfile, keep the manifest change, then regenerate the lockfile with the ecosystem's tool in that directory (e.g. `npm install --package-lock-only --ignore-scripts`). For conflicts in shared manifests (e.g. Directory.Packages.props), keep the highest version of each dependency.",
        "4. If a PR can't be applied cleanly or would make the result unsafe, leave it out and note why rather than forcing it.",
        "5. Where practical, verify the result (e.g. restore/build the affected projects, or `npm ci` in a few of the touched directories). Don't run the whole test suite unless it's quick.",
        "6. Query the complete label set for each included PR with `gh pr view <n> --repo " + store.repo + " --json labels`, build the union of label names, add `dependencies` to that union, and pass every label to `gh pr create` with one `--label \"<name>\"` argument. Do not rely on any truncated label list in this prompt or in the canvas.",
        `7. Commit (keep the Dependabot commit messages or squash with a clear message), push the branch to origin, and open the PR against \`${base}\` with \`gh pr create${request.draft ? " --draft" : ""}\`. The body should summarise the updates, list \`Supersedes #n\` for every PR included, list any excluded PRs with the reason, and mention verification done. (Closing keywords don't close PRs; Dependabot closes its own PRs on its next scheduled run after the base branch has the update, and the user can close them sooner from the canvas.)`,
        "8. Do not approve, merge, close or comment on the original Dependabot PRs — the user does that from the canvas. Remove the temporary worktree and fetched refs when done.",
    );
    if (background) lines.push("", PARALLEL_NOTE);
    lines.push(
        "",
        recordInstruction(
            instanceId,
            "record_combined_pr",
            `{ "requestId": "${request.id}", "prNumber": <new PR number, omit if none was opened>, "included": [<PR numbers included>], "excluded": [{ "number": <n>, "reason": "<why>" }], "summary": "<one or two sentences>" }`,
            { background },
        ),
    );
    if (note) lines.push("", `Additional instructions from the user: ${note}`);
    return lines.join("\n");
}

const filterSchema = {
    q: { type: "string", description: "Free-text filter over title, PR number, directory and dependency names." },
    ecosystem: { type: "string", description: "Ecosystem, e.g. npm, nuget, actions." },
    status: { type: "string", enum: ["", "ready", "needs-review", "failing", "pending", "conflicts", "behind", "draft", "unknown"] },
    updateType: { type: "string", enum: ["", "major", "minor", "patch", "other"] },
    securityOnly: { type: "boolean" },
};

const canvas = createCanvas({
    id: CANVAS_ID,
    displayName: "Dependabot triage",
    description: "Triage open Dependabot PRs: checks, mergeability, related security alerts and overlaps, with sign-off, merge and Dependabot commands.",
    inputSchema: {
        type: "object",
        properties: {
            repo: { type: "string", pattern: REPO_PATTERN, description: "owner/name; defaults to the current repository." },
        },
        additionalProperties: false,
    },
    actions: [
        {
            name: "refresh",
            description: "Reload open Dependabot PRs, mergeability and security alerts from GitHub.",
            handler: async (ctx) => {
                const { store } = instanceFor(ctx);
                await store.refresh();
                return { ok: true, counts: store.snapshot().counts, error: store.error, alertsError: store.alertsError };
            },
        },
        {
            name: "get_summary",
            description: "Counts by status, security alert coverage, biggest overlap clusters and what the user is currently looking at.",
            handler: async (ctx) => {
                const { store, uiState } = instanceFor(ctx);
                const snap = store.snapshot();
                const ecosystems = {};
                for (const p of snap.prs) ecosystems[p.ecosystem] = (ecosystems[p.ecosystem] ?? 0) + 1;
                return {
                    repo: snap.repo,
                    loading: snap.loading,
                    error: snap.error,
                    alertsError: snap.alertsError,
                    lastRefreshed: snap.lastRefreshed,
                    counts: snap.counts,
                    ecosystems,
                    topDependencyClusters: snap.dependencyClusters.slice(0, 10).map((c) => ({ name: c.name, ecosystem: c.ecosystem, targets: c.targets, prCount: c.prs.length, samplePrs: c.prs.slice(0, 5), statuses: c.statuses })),
                    topFileHotspots: snap.fileHotspots.slice(0, 10).map((h) => ({ path: h.path, prCount: h.prs.length, prs: h.prs.slice(0, 10) })),
                    unaddressedAlerts: snap.alerts.filter((a) => !a.fixedBy.length).slice(0, 20).map((a) => ({ number: a.number, severity: a.severity, package: a.package, manifestPath: a.manifestPath, ghsa: a.ghsa })),
                    combined: snap.combined.map((c) => ({ id: c.id, status: c.status, prNumber: c.prNumber, prState: c.prState, prCount: c.numbers.length, included: c.included.length, excluded: c.excluded.length, requestedAt: c.requestedAt })),
                    agents: agents.list(store.repo).map((a) => ({ agentId: a.agentId, name: a.name, kinds: a.kinds, status: a.status, prCount: a.numbers.length, requestIds: a.requestIds, lastPromptAt: a.lastPromptAt, latestIntent: a.latestIntent, error: a.error })),
                    ui: uiState,
                };
            },
        },
        {
            name: "list_prs",
            description: "List open Dependabot PRs (compact rows), optionally filtered.",
            inputSchema: {
                type: "object",
                properties: {
                    ...filterSchema,
                    dependency: { type: "string", description: "Exact dependency name (case-insensitive)." },
                    limit: { type: "integer", minimum: 1, maximum: 500 },
                },
                additionalProperties: false,
            },
            handler: async (ctx) => {
                const { store } = instanceFor(ctx);
                const f = ctx.input ?? {};
                const q = f.q?.toLowerCase();
                const dep = f.dependency?.toLowerCase();
                const rows = store.snapshot().prs.filter((p) =>
                    (!q || [p.title, `#${p.number}`, p.directory, ...p.updates.map((u) => u.name)].join(" ").toLowerCase().includes(q)) &&
                    (!f.ecosystem || p.ecosystem === f.ecosystem) &&
                    (!f.status || p.status === f.status) &&
                    (!f.updateType || p.highestUpdateType === f.updateType) &&
                    (!f.securityOnly || p.security.fixes.length || p.security.related.length) &&
                    (!dep || p.updates.some((u) => u.name.toLowerCase() === dep)),
                );
                return { total: rows.length, prs: rows.slice(0, f.limit ?? 100).map(compactPr) };
            },
        },
        {
            name: "get_pr",
            description: "Full details for one PR, including individual check runs, related alerts and overlaps.",
            inputSchema: { type: "object", properties: { number: { type: "integer" } }, required: ["number"], additionalProperties: false },
            handler: async (ctx) => {
                const { store } = instanceFor(ctx);
                const number = ctx.input.number;
                if (!store.prs.has(number)) throw new CanvasError("pr_not_found", `#${number} is not an open Dependabot PR in ${store.repo}.`);
                const details = await store.getDetails(number);
                const pr = store.getPr(number);
                const snap = store.snapshot();
                return {
                    ...compactPr(pr),
                    title: pr.title,
                    updatesDetailed: pr.updates,
                    files: pr.files,
                    checks: details.checks,
                    recentComments: details.comments.slice(-5),
                    alerts: snap.alerts.filter((a) => a.fixedBy.includes(number) || a.relatedPrs.includes(number)),
                    overlaps: pr.overlaps,
                    agentReview: pr.agentReview,
                };
            },
        },
        {
            name: "get_pr_diff",
            description: "Unified diff for one PR. Lockfiles are summarised unless includeLockfiles is set or a single path is requested.",
            inputSchema: {
                type: "object",
                properties: {
                    number: { type: "integer" },
                    path: { type: "string", description: "Only return the diff for this file." },
                    maxLines: { type: "integer", minimum: 1, maximum: 5000, description: "Line budget across files (default 400)." },
                    includeLockfiles: { type: "boolean" },
                },
                required: ["number"],
                additionalProperties: false,
            },
            handler: async (ctx) => {
                const { store } = instanceFor(ctx);
                const { number, path, maxLines = 400, includeLockfiles = false } = ctx.input;
                if (!store.prs.has(number)) throw new CanvasError("pr_not_found", `#${number} is not an open Dependabot PR in ${store.repo}.`);
                let diff;
                try {
                    diff = await store.getDiff(number);
                } catch (e) {
                    throw new CanvasError("diff_unavailable", e.message.split("\n")[0]);
                }
                if (path && !diff.files.some((f) => f.path === path)) {
                    throw new CanvasError("file_not_in_diff", `${path} is not changed by #${number}. Files: ${diff.files.map((f) => f.path).join(", ")}`);
                }
                const { text, omitted } = formatDiff(diff, { path, maxLines, includeLockfiles });
                return {
                    number,
                    headOid: diff.headOid,
                    files: diff.files.map((f) => ({ path: f.path, status: f.status, additions: f.additions, deletions: f.deletions, lockfile: f.lockfile, binary: f.binary })),
                    omitted,
                    diff: text,
                };
            },
        },
        {
            name: "focus_pr",
            description: "Select and show a PR in the canvas UI.",
            inputSchema: { type: "object", properties: { number: { type: "integer" } }, required: ["number"], additionalProperties: false },
            handler: async (ctx) => {
                const { server } = instanceFor(ctx);
                server.sendCommand({ type: "focus", number: ctx.input.number });
                return { ok: true };
            },
        },
        {
            name: "select_prs",
            description: "Tick PRs in the canvas so the user can apply a bulk action (the user still confirms any approve/merge).",
            inputSchema: {
                type: "object",
                properties: { numbers: { type: "array", items: { type: "integer" } }, replace: { type: "boolean", description: "Replace the current selection (default true)." } },
                required: ["numbers"],
                additionalProperties: false,
            },
            handler: async (ctx) => {
                const { server } = instanceFor(ctx);
                server.sendCommand({ type: "select", numbers: ctx.input.numbers, replace: ctx.input.replace !== false });
                return { ok: true, selected: ctx.input.numbers.length };
            },
        },
        {
            name: "set_filters",
            description: "Change the canvas filters, grouping or tab.",
            inputSchema: {
                type: "object",
                properties: {
                    ...filterSchema,
                    groupBy: { type: "string", enum: ["dependency", "directory", "ecosystem", "status", "none"] },
                    tab: { type: "string", enum: ["prs", "alerts", "overlaps"] },
                },
                additionalProperties: false,
            },
            handler: async (ctx) => {
                const { server } = instanceFor(ctx);
                server.sendCommand({ type: "filters", ...ctx.input });
                return { ok: true };
            },
        },
        {
            name: "record_review",
            description: "Record the agent's review verdict for one or more PRs so it shows in the canvas.",
            inputSchema: {
                type: "object",
                properties: {
                    numbers: { type: "array", items: { type: "integer" }, minItems: 1 },
                    verdict: { type: "string", enum: ["safe", "caution", "blocked"] },
                    summary: { type: "string", maxLength: 4000 },
                },
                required: ["numbers", "verdict", "summary"],
                additionalProperties: false,
            },
            handler: async (ctx) => {
                const { store } = instanceFor(ctx);
                await store.recordReview(ctx.input.numbers, { verdict: ctx.input.verdict, summary: ctx.input.summary });
                return { ok: true };
            },
        },
        {
            name: "record_combined_pr",
            description: "Record the pull request the agent opened to replace a group of Dependabot PRs (from a canvas combine request), so the canvas can mark the originals as superseded.",
            inputSchema: {
                type: "object",
                properties: {
                    requestId: { type: "string", maxLength: 100, description: "Combine request id from the prompt." },
                    prNumber: { type: "integer", minimum: 1, description: "The combined PR; omit if none was opened." },
                    included: { type: "array", items: { type: "integer" }, description: "Dependabot PRs whose changes are in the combined PR." },
                    excluded: {
                        type: "array",
                        items: { type: "object", properties: { number: { type: "integer" }, reason: { type: "string", maxLength: 500 } }, required: ["number"], additionalProperties: false },
                    },
                    summary: { type: "string", maxLength: 4000 },
                },
                additionalProperties: false,
            },
            handler: async (ctx) => {
                const { store } = instanceFor(ctx);
                if (!ctx.input?.requestId && !ctx.input?.included?.length) throw new CanvasError("invalid_input", "Pass requestId, or the included PR numbers.");
                try {
                    const rec = await store.recordCombinedPr(ctx.input);
                    return { ok: true, id: rec.id, status: rec.status, prNumber: rec.prNumber, prState: rec.prState, included: rec.included.length, excluded: rec.excluded.length };
                } catch (e) {
                    throw new CanvasError("record_failed", e.message.split("\n")[0]);
                }
            },
        },
    ],
    open: async (ctx) => {
        const repo = await resolveRepo(ctx.input);
        let entry = instances.get(ctx.instanceId);
        if (entry && entry.store.repo.toLowerCase() !== repo.toLowerCase()) {
            instances.delete(ctx.instanceId);
            await entry.server.close();
            entry = null;
        }
        if (!entry) {
            const store = getStore(repo);
            const instanceId = ctx.instanceId;
            entry = { store, uiState: null, server: null };
            const created = entry;
            entry.server = await startInstanceServer({
                instanceId,
                store,
                agents,
                onSendToSession: async ({ numbers, note, target }) => {
                    const prs = numbers.map((n) => store.getPr(n)).filter(Boolean);
                    const deps = [...new Set(prs.flatMap((p) => p.updates.map((u) => u.name)))];
                    const label = numbers.length === 1 ? `#${numbers[0]}` : deps.length === 1 ? `${deps[0]} (${numbers.length} PRs)` : `${numbers.length} PRs`;
                    return dispatch({
                        store,
                        target,
                        kind: "review",
                        numbers,
                        name: `Review ${label}`.slice(0, 80),
                        description: `Review Dependabot ${numbers.length === 1 ? "PR" : "PRs"} ${numbers.slice(0, 10).map((n) => `#${n}`).join(", ")}${numbers.length > 10 ? "…" : ""}`,
                        buildPrompt: (opts) => buildReviewPrompt(store, instanceId, numbers, note, opts),
                    });
                },
                onCombine: async ({ request, note, target }) =>
                    dispatch({
                        store,
                        target,
                        kind: "combine",
                        numbers: request.numbers,
                        requestId: request.id,
                        name: `Combine: ${request.title}`.slice(0, 80),
                        description: `Combine ${request.numbers.length} Dependabot PRs into ${request.branch}`,
                        buildPrompt: (opts) => buildCombinePrompt(store, instanceId, request, note, opts),
                    }),
                onUiState: (state) => {
                    created.uiState = state;
                },
            });
            instances.set(instanceId, entry);
            if (!store.lastRefreshed || Date.now() - Date.parse(store.lastRefreshed) > AUTO_REFRESH_MS) store.refresh();
        }
        const counts = entry.store.snapshot().counts;
        return {
            title: `Dependabot · ${repo}`,
            status: entry.store.lastRefreshed ? `${counts.open} open` : "Loading…",
            url: entry.server.url,
        };
    },
    onClose: async (ctx) => {
        const entry = instances.get(ctx.instanceId);
        if (entry) {
            instances.delete(ctx.instanceId);
            await entry.server.close();
        }
    },
});

session = await joinSession({ canvases: [canvas] });
agents.attach(session);

setInterval(() => {
    const stores = new Set([...instances.values()].map((e) => e.store));
    for (const store of stores) {
        if (!store.loading.list && store.lastRefreshed && Date.now() - Date.parse(store.lastRefreshed) > AUTO_REFRESH_MS) store.refresh();
    }
}, 60_000).unref();
