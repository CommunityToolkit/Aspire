// Tracks background agents (sub-sessions) the canvas starts via the runtime's
// tasks API, so reviews and combines can run in parallel with the main session
// and each other. Agent ids are session-scoped, so state lives in the session
// workspace rather than the per-user artifacts folder.

import { EventEmitter } from "node:events";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { join } from "node:path";

const ACTIVE = new Set(["running", "idle"]);
const RESULT_BLOCK = /```dependabot-canvas[^\n]*\n([\s\S]*?)```/g;
const POLL_ACTIVE_MS = 8_000;
const MAX_TEXT = 4_000;
const MAX_AGENTS = 50;
const RESULT_RETRY_LIMIT = 5;

function clip(text, max = MAX_TEXT) {
    if (typeof text !== "string") return null;
    return text.length > max ? `${text.slice(0, max)}…` : text;
}

/** Extracts `{ action, input }` payloads from ```dependabot-canvas fenced blocks. */
export function parseResultBlocks(text) {
    const out = [];
    if (typeof text !== "string") return out;
    for (const m of text.matchAll(RESULT_BLOCK)) {
        try {
            const value = JSON.parse(m[1]);
            for (const item of Array.isArray(value) ? value : [value]) {
                if (item && typeof item.action === "string" && item.input && typeof item.input === "object") out.push({ action: item.action, input: item.input, raw: JSON.stringify(item) });
            }
        } catch {
            // Ignore malformed blocks; the agent may also have called the canvas action directly.
        }
    }
    return out;
}

export class AgentResultValidationError extends Error {
    constructor(message) {
        super(message);
        this.name = "AgentResultValidationError";
        this.permanent = true;
    }
}

export class AgentTracker extends EventEmitter {
    constructor() {
        super();
        this.session = null;
        this.agents = new Map();
        this.path = null;
        this._pollTimer = null;
        this._polling = null;
        this._loaded = Promise.resolve();
        this._resultHandler = null;
    }

    /** Binds the tracker to the joined session; call once after joinSession. */
    attach(session) {
        this.session = session;
        this.path = session.workspacePath ? join(session.workspacePath, "files", "dependabot-triage-agents.json") : null;
        this._loaded = this._load();
        session.on((event) => {
            if (event.type?.startsWith("subagent.") || event.type === "session.background_tasks_changed") this.schedulePoll(250);
        });
        return this._loaded;
    }

    _requireSession() {
        if (!this.session) throw Object.assign(new Error("The canvas is still connecting to the session; try again in a moment."), { status: 503 });
        return this.session;
    }

    async _load() {
        if (!this.path) return;
        try {
            const list = JSON.parse(await readFile(this.path, "utf8"));
            for (const a of list) this.agents.set(a.agentId, a);
        } catch {
            // First run in this session.
        }
        this.emit("change");
        if ([...this.agents.values()].some((a) => ACTIVE.has(a.status))) this.schedulePoll(500);
    }

    async _save() {
        this.emit("change");
        if (!this.path) return;
        await mkdir(join(this.path, ".."), { recursive: true });
        await writeFile(this.path, JSON.stringify([...this.agents.values()], null, 2));
    }

    get(agentId) {
        return this.agents.get(agentId) ?? null;
    }

    list(repo) {
        return [...this.agents.values()]
            .filter((a) => !repo || a.repo?.toLowerCase() === repo.toLowerCase())
            .sort((a, b) => Date.parse(b.lastPromptAt) - Date.parse(a.lastPromptAt))
            .map(({ applied, resultFailures, ...rest }) => rest);
    }

    setResultHandler(handler) {
        this._resultHandler = handler;
        return () => {
            if (this._resultHandler === handler) this._resultHandler = null;
        };
    }

    isActive(agentId) {
        return ACTIVE.has(this.agents.get(agentId)?.status);
    }

    async start({ repo, kind, name, description, prompt, numbers, requestId }) {
        await this._loaded;
        const { agentId } = await this._requireSession().rpc.tasks.startAgent({ agentType: "general-purpose", prompt, name, description });
        const now = new Date().toISOString();
        this.agents.set(agentId, {
            agentId,
            repo,
            name,
            description,
            kinds: [kind],
            numbers: [...numbers],
            requestIds: requestId ? [requestId] : [],
            history: [{ kind, numbers: [...numbers], requestId: requestId ?? null, at: now }],
            status: "running",
            startedAt: now,
            lastPromptAt: now,
            completedAt: null,
            latestIntent: null,
            latestResponse: null,
            error: null,
            applied: [],
        });
        this._trim();
        await this._save();
        this.schedulePoll(2_000);
        return this.agents.get(agentId);
    }

    async message(agentId, { prompt, kind, numbers = [], requestId }) {
        await this._loaded;
        const rec = this.agents.get(agentId);
        if (!rec) throw Object.assign(new Error("Unknown agent."), { status: 404 });
        const res = await this._requireSession().rpc.tasks.sendMessage({ id: agentId, message: prompt });
        if (!res.sent) throw Object.assign(new Error(res.error || "The agent did not accept the message (it may have finished)."), { status: 409 });
        const now = new Date().toISOString();
        rec.numbers = [...new Set([...rec.numbers, ...numbers])];
        if (!rec.kinds.includes(kind)) rec.kinds.push(kind);
        if (requestId) rec.requestIds.push(requestId);
        rec.history.push({ kind, numbers: [...numbers], requestId: requestId ?? null, at: now });
        rec.history = rec.history.slice(-20);
        rec.status = "running";
        rec.lastPromptAt = now;
        rec.awaitingSince = now;
        rec.error = null;
        await this._save();
        this.schedulePoll(2_000);
        return rec;
    }

    async cancel(agentId) {
        const rec = this.agents.get(agentId);
        if (!rec) throw Object.assign(new Error("Unknown agent."), { status: 404 });
        const { cancelled } = await this._requireSession().rpc.tasks.cancel({ id: agentId });
        this.schedulePoll(250);
        return cancelled;
    }

    async dismiss(agentId) {
        const rec = this.agents.get(agentId);
        if (!rec) return;
        if (rec.status === "running") throw Object.assign(new Error("Cancel the agent before dismissing it."), { status: 409 });
        try {
            await this._requireSession().rpc.tasks.remove({ id: agentId });
        } catch {
            // Already gone from the runtime.
        }
        this.agents.delete(agentId);
        await this._save();
    }

    _trim() {
        const inactive = [...this.agents.values()].filter((a) => !ACTIVE.has(a.status)).sort((a, b) => Date.parse(a.lastPromptAt) - Date.parse(b.lastPromptAt));
        while (this.agents.size > MAX_AGENTS && inactive.length) this.agents.delete(inactive.shift().agentId);
    }

    schedulePoll(delay) {
        clearTimeout(this._pollTimer);
        this._pollTimer = setTimeout(() => {
            this._pollTimer = null;
            this.poll().catch((e) => {
                process.stderr.write(`[dependabot-triage] agent poll failed: ${e?.stack ?? e}\n`);
                if (!this._pollTimer && [...this.agents.values()].some((a) => a.status === "running")) this.schedulePoll(POLL_ACTIVE_MS);
            });
        }, delay);
        this._pollTimer.unref?.();
    }

    /** Polls the runtime once; a request made while a poll is in flight queues one more pass. */
    async poll() {
        if (this._polling) {
            this._repoll = true;
            return this._polling;
        }
        this._polling = (async () => {
            try {
                do {
                    this._repoll = false;
                    await this._poll();
                } while (this._repoll);
            } finally {
                this._polling = null;
            }
        })();
        return this._polling;
    }

    async _poll() {
        await this._loaded;
        if (!this.session || !this.agents.size) return;
        const { tasks } = await this._requireSession().rpc.tasks.list();
        const byId = new Map(tasks.filter((t) => t.type === "agent").map((t) => [t.id, t]));
        let dirty = false;
        let retryResults = false;
        for (const rec of this.agents.values()) {
            const t = byId.get(rec.agentId);
            const before = JSON.stringify([rec.status, rec.latestIntent, rec.latestResponse, rec.error, rec.completedAt]);
            if (!t) {
                if (ACTIVE.has(rec.status)) rec.status = "gone";
            } else {
                // After a follow-up, the runtime can still report the previous idle state
                // until it picks the message up; keep treating the agent as running.
                const staleIdle = t.status === "idle" && rec.awaitingSince && (!t.idleSince || Date.parse(t.idleSince) <= Date.parse(rec.awaitingSince)) && Date.now() - Date.parse(rec.awaitingSince) < 120_000;
                if (staleIdle) {
                    if (rec.status !== "running") dirty = true;
                    rec.status = "running";
                    continue;
                }
                rec.awaitingSince = null;
                rec.status = t.status;
                if (t.status !== "running") rec.latestIntent = null;
                rec.error = t.error ?? null;
                rec.completedAt = t.completedAt ?? null;
                rec.idleSince = t.idleSince ?? null;
                const text = t.latestResponse ?? t.result ?? null;
                if (text) rec.latestResponse = clip(text);
                const resultText = [t.latestResponse, t.result].filter((v) => typeof v === "string").join("\n");
                if (t.status === "running") {
                    try {
                        const { progress } = await this._requireSession().rpc.tasks.getProgress({ id: rec.agentId });
                        if (progress?.type === "agent") rec.latestIntent = clip(progress.latestIntent ?? progress.recentActivity?.at(-1)?.message ?? rec.latestIntent, 300);
                    } catch {
                        // Progress is best-effort.
                    }
                } else if (resultText) {
                    for (const payload of parseResultBlocks(resultText)) {
                        if (rec.applied.includes(payload.raw)) continue;
                        try {
                            if (!this._resultHandler) throw new Error("No result handler is registered.");
                            await this._resultHandler({ agent: rec, action: payload.action, input: payload.input });
                            rec.applied.push(payload.raw);
                            if (rec.resultFailures) delete rec.resultFailures[payload.raw];
                            dirty = true;
                        } catch (e) {
                            const permanent = e instanceof AgentResultValidationError || e?.permanent === true;
                            if (permanent) {
                                process.stderr.write(`[dependabot-triage] ignored invalid result from agent ${rec.agentId}: ${e?.message ?? e}\n`);
                                rec.applied.push(payload.raw);
                                if (rec.resultFailures) delete rec.resultFailures[payload.raw];
                                dirty = true;
                            } else {
                                rec.resultFailures ??= {};
                                const attempts = (rec.resultFailures[payload.raw] ?? 0) + 1;
                                rec.resultFailures[payload.raw] = attempts;
                                dirty = true;
                                if (attempts >= RESULT_RETRY_LIMIT) {
                                    process.stderr.write(`[dependabot-triage] gave up applying result from agent ${rec.agentId} after ${attempts} attempts: ${e?.stack ?? e}\n`);
                                    rec.applied.push(payload.raw);
                                    delete rec.resultFailures[payload.raw];
                                } else {
                                    retryResults = true;
                                    process.stderr.write(`[dependabot-triage] result from agent ${rec.agentId} failed, will retry (${attempts}/${RESULT_RETRY_LIMIT}): ${e?.stack ?? e}\n`);
                                }
                            }
                        }
                        rec.applied = rec.applied.slice(-50);
                    }
                }
            }
            if (JSON.stringify([rec.status, rec.latestIntent, rec.latestResponse, rec.error, rec.completedAt]) !== before) dirty = true;
        }
        if (dirty) await this._save();
        if (retryResults || [...this.agents.values()].some((a) => a.status === "running")) this.schedulePoll(POLL_ACTIVE_MS);
    }
}
