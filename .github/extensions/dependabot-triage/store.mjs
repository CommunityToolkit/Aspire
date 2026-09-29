// Per-repository state shared by every open canvas instance for that repo.

import { EventEmitter } from "node:events";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { homedir } from "node:os";
import { join } from "node:path";
import * as gh from "./github.mjs";
import { countBy, deriveState, normalizeAlert, normalizePr, parseUnifiedDiff } from "./model.mjs";

const MERGEABILITY_BATCH = 20;
const MERGEABILITY_CONCURRENCY = 3;
const MUTATION_SPACING_MS = 750;
const HIDE_RECENTLY_CLOSED_MS = 10 * 60_000;
const DIFF_TTL_MS = 10 * 60_000;
const DIFF_CACHE_SIZE = 25;
const COMBINE_STALE_MS = 7 * 24 * 60 * 60_000;

// Dependabot comment commands still supported after the Jan 2026 retirement of
// merge/close/reopen commands. Merge/close go through native `gh` instead.
export const DEPENDABOT_COMMANDS = {
    rebase: { label: "Rebase", body: () => "@dependabot rebase" },
    recreate: { label: "Recreate (discard edits)", body: () => "@dependabot recreate" },
    "ignore-dependency": { label: "Ignore this dependency", body: () => "@dependabot ignore this dependency" },
    "ignore-major": { label: "Ignore this major version", body: () => "@dependabot ignore this major version" },
    "ignore-minor": { label: "Ignore this minor version", body: () => "@dependabot ignore this minor version" },
    "ignore-patch": { label: "Ignore this patch version", body: () => "@dependabot ignore this patch version" },
    "show-ignore": { label: "Show ignore conditions", body: (dep) => `@dependabot show ${dep} ignore conditions`, needsDependency: true },
    "ignore-named": { label: "Ignore dependency (grouped PR)", body: (dep) => `@dependabot ignore ${dep}`, needsDependency: true },
    "unignore-named": { label: "Unignore dependency", body: (dep) => `@dependabot unignore ${dep}`, needsDependency: true },
};

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function copilotHome() {
    return process.env.COPILOT_HOME || join(homedir(), ".copilot");
}

async function mapLimit(items, limit, fn) {
    const queue = [...items];
    const workers = Array.from({ length: Math.min(limit, queue.length) }, async () => {
        while (queue.length) await fn(queue.shift());
    });
    await Promise.all(workers);
}

export class RepoStore extends EventEmitter {
    constructor(repo) {
        super();
        this.setMaxListeners(100);
        this.repo = repo;
        this.repoInfo = null;
        this.prs = new Map();
        this.alerts = null;
        this.alertsError = null;
        this.error = null;
        this.loading = { list: false, mergeability: false, alerts: false, progress: null };
        this.lastRefreshed = null;
        this.details = new Map();
        this.diffs = new Map();
        this.diffsInFlight = new Map();
        this.reviews = {};
        this.recentlyClosed = new Map();
        this.jobs = new Map();
        this._refreshPromise = null;
        this._snapshot = null;
        this._emitTimer = null;
        this._jobSeq = 0;
        this.reviewsPath = join(copilotHome(), "extensions", "dependabot-triage", "artifacts", repo.replace("/", "__"), "reviews.json");
        this._reviewsLoaded = this._loadReviews();
        this.combined = {};
        this.combinedPath = join(this.reviewsPath, "..", "combined.json");
        this._combinedLoaded = this._loadCombined();
    }

    // --- change notification --------------------------------------------

    _changed() {
        this._snapshot = null;
        if (this._emitTimer) return;
        this._emitTimer = setTimeout(() => {
            this._emitTimer = null;
            this.emit("change");
        }, 300);
    }

    snapshot() {
        if (this._snapshot) return this._snapshot;
        const now = Date.now();
        for (const [n, t] of this.recentlyClosed) if (now - t > HIDE_RECENTLY_CLOSED_MS) this.recentlyClosed.delete(n);
        const prs = [...this.prs.values()].filter((p) => !this.recentlyClosed.has(p.number));
        const derived = deriveState({ prs, alerts: this.alerts ?? [], reviews: this.reviews });
        const combined = Object.values(this.combined).sort((a, b) => b.requestedAt.localeCompare(a.requestedAt));
        for (const pr of derived.prs) {
            const rec = combined.find((c) => (c.status === "opened" ? c.included.includes(pr.number) : c.status === "requested" && c.numbers.includes(pr.number)));
            pr.combine = rec ? { id: rec.id, status: rec.status, prNumber: rec.prNumber, prUrl: rec.prUrl, prState: rec.prState } : null;
        }
        const counts = countBy(derived.prs.map((p) => p.status));
        this._snapshot = {
            repo: this.repo,
            repoInfo: this.repoInfo,
            loading: this.loading,
            error: this.error,
            alertsError: this.alertsError,
            lastRefreshed: this.lastRefreshed,
            counts: {
                open: derived.prs.length,
                ...counts,
                security: derived.prs.filter((p) => p.security.fixes.length).length,
                alerts: derived.alerts.length,
                unaddressedAlerts: derived.alerts.filter((a) => !a.fixedBy.length).length,
            },
            dependabotCommands: Object.fromEntries(Object.entries(DEPENDABOT_COMMANDS).map(([k, v]) => [k, { label: v.label, needsDependency: !!v.needsDependency }])),
            combined,
            ...derived,
        };
        return this._snapshot;
    }

    getPr(number) {
        return this.snapshot().prs.find((p) => p.number === number) ?? null;
    }

    // --- loading ----------------------------------------------------------

    refresh() {
        if (this._refreshPromise) return this._refreshPromise;
        this._refreshPromise = this._doRefresh().finally(() => {
            this._refreshPromise = null;
        });
        return this._refreshPromise;
    }

    async _doRefresh() {
        this.error = null;
        this.loading = { list: true, mergeability: false, alerts: true, progress: { loaded: 0, total: null } };
        this._changed();
        await this._reviewsLoaded;

        const alertsTask = this._loadAlerts();
        try {
            if (!this.repoInfo) this.repoInfo = await gh.fetchRepoInfo(this.repo);
            const seen = new Set();
            await gh.listDependabotPrs(this.repo, (page, progress) => {
                for (const node of page) {
                    const next = normalizePr(node);
                    const prev = this.prs.get(next.number);
                    if (prev && prev.headOid === next.headOid) {
                        next.mergeable = prev.mergeable;
                        next.mergeStateStatus = prev.mergeStateStatus;
                    }
                    this.prs.set(next.number, next);
                    seen.add(next.number);
                }
                this.loading = { ...this.loading, progress };
                this._changed();
            });
            for (const n of [...this.prs.keys()]) if (!seen.has(n)) this.prs.delete(n);
            this.loading = { ...this.loading, list: false, mergeability: true };
            this._changed();
            await Promise.all([this._loadMergeability([...this.prs.values()]), this._refreshCombined()]);
        } catch (e) {
            this.error = e.message;
        } finally {
            this.loading = { ...this.loading, list: false, mergeability: false };
            this.lastRefreshed = new Date().toISOString();
            this._changed();
        }
        await alertsTask;
    }

    async _loadAlerts() {
        try {
            const raw = await gh.fetchOpenAlerts(this.repo);
            this.alerts = raw.map(normalizeAlert);
            this.alertsError = null;
        } catch (e) {
            this.alertsError = /403|404|not accessible|disabled/i.test(e.message)
                ? `Dependabot alerts unavailable (needs security alert read access, and alerts must be enabled): ${e.message.split("\n")[0]}`
                : e.message.split("\n")[0];
        } finally {
            this.loading = { ...this.loading, alerts: false };
            this._changed();
        }
    }

    async _loadMergeability(prs, attempt = 0) {
        const batches = [];
        for (let i = 0; i < prs.length; i += MERGEABILITY_BATCH) batches.push(prs.slice(i, i + MERGEABILITY_BATCH));
        await mapLimit(batches, MERGEABILITY_CONCURRENCY, async (batch) => {
            try {
                const nodes = await gh.fetchMergeability(batch.map((p) => p.id));
                for (const n of nodes) {
                    const pr = this.prs.get(n.number);
                    if (!pr) continue;
                    if (n.state && n.state !== "OPEN") {
                        this.prs.delete(n.number);
                        continue;
                    }
                    pr.mergeable = n.mergeable;
                    pr.mergeStateStatus = n.mergeStateStatus;
                    pr.reviewDecision = n.reviewDecision;
                }
                this._changed();
            } catch {
                // Leave as UNKNOWN; details view fetches individually.
            }
        });
        // GitHub computes mergeability lazily: the first query kicks off the
        // computation and often returns UNKNOWN. Retry those a couple of times.
        const pending = prs.filter((p) => this.prs.get(p.number)?.mergeable === "UNKNOWN");
        if (pending.length && attempt < 2) {
            await new Promise((r) => setTimeout(r, 4000 * (attempt + 1)));
            await this._loadMergeability(pending, attempt + 1);
        }
    }

    async refreshPrs(numbers) {
        const prs = numbers.map((n) => this.prs.get(n)).filter(Boolean);
        if (prs.length) await this._loadMergeability(prs);
    }

    async getDetails(number, { force = false } = {}) {
        const cached = this.details.get(number);
        if (!force && cached && Date.now() - Date.parse(cached.fetchedAt) < 60_000) return cached;
        const details = await gh.fetchPrDetails(this.repo, number);
        this.details.set(number, details);
        const pr = this.prs.get(number);
        if (pr) {
            if (details.state !== "OPEN") {
                this.recentlyClosed.set(number, Date.now());
            }
            pr.mergeable = details.mergeable;
            pr.mergeStateStatus = details.mergeStateStatus;
            pr.reviewDecision = details.reviewDecision;
            pr.autoMerge = details.autoMerge;
            pr.checks = details.checksState;
            pr.headOid = details.headOid ?? pr.headOid;
            if (details.files.length > pr.files.length || pr.filesTruncated) {
                pr.files = details.files.map((f) => f.path);
                pr.filesTruncated = !!details.filesTruncated;
            }
            this._changed();
        }
        return details;
    }

    async getDiff(number, { force = false } = {}) {
        const headOid = this.prs.get(number)?.headOid ?? null;
        const cached = this.diffs.get(number);
        const fresh = cached && (!headOid || cached.headOid === headOid) && Date.now() - Date.parse(cached.fetchedAt) < DIFF_TTL_MS;
        if (!force && fresh) return cached;
        if (this.diffsInFlight.has(number)) return this.diffsInFlight.get(number);
        const task = (async () => {
            const text = await gh.fetchPrDiff(this.repo, number);
            const diff = { number, headOid, fetchedAt: new Date().toISOString(), ...parseUnifiedDiff(text) };
            this.diffs.delete(number);
            this.diffs.set(number, diff);
            while (this.diffs.size > DIFF_CACHE_SIZE) this.diffs.delete(this.diffs.keys().next().value);
            return diff;
        })().finally(() => this.diffsInFlight.delete(number));
        this.diffsInFlight.set(number, task);
        return task;
    }

    // --- agent review notes (persisted per user, per repo) -----------------

    async _loadReviews() {
        try {
            this.reviews = JSON.parse(await readFile(this.reviewsPath, "utf8"));
        } catch {
            this.reviews = {};
        }
    }

    async recordReview(numbers, { verdict, summary, reviewer }) {
        await this._reviewsLoaded;
        const recordedAt = new Date().toISOString();
        for (const n of numbers) {
            const pr = this.prs.get(n);
            this.reviews[n] = { verdict, summary, reviewer: reviewer ?? "agent", headOid: pr?.headOid ?? null, recordedAt };
        }
        await mkdir(join(this.reviewsPath, ".."), { recursive: true });
        await writeFile(this.reviewsPath, JSON.stringify(this.reviews, null, 2));
        this._changed();
    }

    async clearReview(numbers) {
        await this._reviewsLoaded;
        for (const n of numbers) delete this.reviews[n];
        await mkdir(join(this.reviewsPath, ".."), { recursive: true });
        await writeFile(this.reviewsPath, JSON.stringify(this.reviews, null, 2));
        this._changed();
    }

    // --- combine requests (agent folds several PRs into one) ---------------

    async _loadCombined() {
        try {
            this.combined = JSON.parse(await readFile(this.combinedPath, "utf8"));
        } catch {
            this.combined = {};
        }
    }

    async _saveCombined() {
        await mkdir(join(this.combinedPath, ".."), { recursive: true });
        await writeFile(this.combinedPath, JSON.stringify(this.combined, null, 2));
        this._changed();
    }

    async createCombineRequest({ numbers, branch, title, base, draft, fixBreakages }) {
        await this._combinedLoaded;
        const now = new Date();
        const id = `combine-${now.getTime().toString(36)}`;
        this.combined[id] = {
            id,
            numbers,
            branch,
            title,
            base,
            draft: !!draft,
            fixBreakages: !!fixBreakages,
            headOids: Object.fromEntries(numbers.map((n) => [n, this.prs.get(n)?.headOid ?? null])),
            requestedAt: now.toISOString(),
            status: "requested",
            prNumber: null,
            prUrl: null,
            prTitle: null,
            prState: null,
            included: [],
            excluded: [],
            summary: null,
        };
        await this._saveCombined();
        return this.combined[id];
    }

    async recordCombinedPr({ requestId, prNumber, included, excluded, summary }) {
        await this._combinedLoaded;
        let rec = requestId ? this.combined[requestId] : null;
        if (requestId && !rec) throw new Error(`Unknown combine request ${requestId}.`);
        if (!rec) {
            if (!included?.length) throw new Error("Pass requestId, or the included PR numbers.");
            rec = await this.createCombineRequest({ numbers: included, branch: null, title: null, base: null });
        }
        const now = new Date().toISOString();
        if (prNumber) {
            const info = await gh.fetchPrState(this.repo, prNumber);
            Object.assign(rec, {
                status: "opened",
                prNumber: info.number,
                prUrl: info.url,
                prTitle: info.title,
                prState: info.mergedAt ? "MERGED" : info.state,
                prCheckedAt: now,
                branch: rec.branch ?? info.headRefName,
            });
        } else {
            rec.status = "failed";
        }
        rec.included = (included?.length ? included : rec.numbers).filter((n) => rec.numbers.includes(n) || !requestId);
        rec.excluded = (excluded ?? []).map((e) => ({ number: e.number, reason: String(e.reason ?? "").slice(0, 500) }));
        rec.summary = summary ? String(summary).slice(0, 4000) : null;
        rec.recordedAt = now;
        await this._saveCombined();
        return rec;
    }

    async clearCombined(id) {
        await this._combinedLoaded;
        delete this.combined[id];
        await this._saveCombined();
    }

    async _refreshCombined() {
        await this._combinedLoaded;
        const now = Date.now();
        let dirty = false;
        for (const rec of Object.values(this.combined)) {
            if (rec.status === "opened" && rec.prState === "OPEN") {
                try {
                    const info = await gh.fetchPrState(this.repo, rec.prNumber);
                    rec.prState = info.mergedAt ? "MERGED" : info.state;
                    rec.prTitle = info.title;
                    rec.prCheckedAt = new Date().toISOString();
                    dirty = true;
                } catch {
                    // Keep the last known state.
                }
            }
            const age = now - Date.parse(rec.recordedAt ?? rec.requestedAt);
            const anyOpen = (rec.status === "opened" ? rec.included : rec.numbers).some((n) => this.prs.has(n));
            const finished = rec.status !== "opened" || rec.prState !== "OPEN";
            if ((finished && !anyOpen) || (rec.status !== "opened" && age > COMBINE_STALE_MS)) {
                delete this.combined[rec.id];
                dirty = true;
            }
        }
        if (dirty) await this._saveCombined();
    }

    // --- mutations (always human-initiated from the canvas UI) --------------

    startJob(type, numbers, options = {}) {
        const job = {
            id: `job-${++this._jobSeq}`,
            type,
            numbers,
            options,
            status: "running",
            startedAt: new Date().toISOString(),
            results: [],
        };
        this.jobs.set(job.id, job);
        this.emit("job", job);
        this._runJob(job).catch((e) => {
            job.status = "error";
            job.error = e.message;
            this.emit("job", job);
        });
        return job;
    }

    async _runJob(job) {
        const { type, numbers, options } = job;
        for (let i = 0; i < numbers.length; i++) {
            const number = numbers[i];
            const pr = this.prs.get(number);
            try {
                const message = await this._runStep(type, number, pr, options);
                job.results.push({ number, ok: true, message });
            } catch (e) {
                job.results.push({ number, ok: false, message: e.message.split("\n").slice(0, 3).join(" ") });
            }
            this.emit("job", job);
            if (i < numbers.length - 1) await sleep(MUTATION_SPACING_MS);
        }
        job.status = "done";
        job.finishedAt = new Date().toISOString();
        this.emit("job", job);

        for (const n of numbers) this.details.delete(n);
        if (type === "merge" || type === "approve-merge" || type === "close") {
            // Merged/closed PRs linger in search results briefly; re-check siblings that shared files.
            await sleep(1500);
            this.refresh();
        } else {
            await this.refreshPrs(numbers);
        }
    }

    async _runStep(type, number, pr, options) {
        const repo = this.repo;
        switch (type) {
            case "approve":
                await gh.approvePr(repo, number, options.body);
                if (pr) pr.reviewDecision = "APPROVED";
                this._changed();
                return "Approved";
            case "merge":
            case "approve-merge": {
                if (type === "approve-merge" && pr?.reviewDecision !== "APPROVED") {
                    await gh.approvePr(repo, number, options.body);
                    if (pr) pr.reviewDecision = "APPROVED";
                }
                const out = await gh.mergePr(repo, number, {
                    method: options.method,
                    auto: !!options.auto,
                    deleteBranch: !!options.deleteBranch,
                    headOid: options.matchHead === false ? undefined : pr?.headOid,
                });
                if (options.auto) {
                    if (pr) pr.autoMerge = { method: options.method.toUpperCase() };
                    this._changed();
                    return (out.trim() || "Auto-merge enabled").split("\n")[0];
                }
                this.recentlyClosed.set(number, Date.now());
                this._changed();
                return (out.trim() || "Merged").split("\n")[0];
            }
            case "disable-auto":
                await gh.disableAutoMerge(repo, number);
                if (pr) pr.autoMerge = null;
                this._changed();
                return "Auto-merge disabled";
            case "dependabot": {
                const cmd = DEPENDABOT_COMMANDS[options.command];
                if (!cmd) throw new Error(`Unknown Dependabot command: ${options.command}`);
                const dep = options.dependency || (cmd.needsDependency ? pr?.updates?.[0]?.name : undefined);
                if (cmd.needsDependency && !dep) throw new Error("This command needs a dependency name.");
                const body = cmd.body(dep);
                await gh.commentOnPr(repo, number, body);
                return `Commented "${body}"`;
            }
            case "close":
                await gh.closePr(repo, number, options.comment);
                this.recentlyClosed.set(number, Date.now());
                this._changed();
                return "Closed";
            default:
                throw new Error(`Unknown action: ${type}`);
        }
    }
}

const stores = new Map();

export function getStore(repo) {
    let store = stores.get(repo.toLowerCase());
    if (!store) {
        store = new RepoStore(repo);
        stores.set(repo.toLowerCase(), store);
    }
    return store;
}
