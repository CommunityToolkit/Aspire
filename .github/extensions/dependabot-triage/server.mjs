// Per-instance loopback HTTP server that serves the canvas UI and a small
// token-protected JSON/SSE API backed by the shared RepoStore.

import { randomBytes, timingSafeEqual } from "node:crypto";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const UI_DIR = join(dirname(fileURLToPath(import.meta.url)), "ui");
const STATIC = {
    "/app.js": "text/javascript; charset=utf-8",
    "/styles.css": "text/css; charset=utf-8",
};
const MAX_BODY = 256 * 1024;
const ACTION_TYPES = new Set(["approve", "merge", "approve-merge", "disable-auto", "dependabot", "close"]);
const INVALID_BRANCH_CHARS_RE = /[\x00-\x20~^:?*\[\\]/;
const SAFE_BRANCH_CHARS_RE = /^[A-Za-z0-9][A-Za-z0-9._/-]*$/;

function safeEqual(a, b) {
    const x = Buffer.from(String(a ?? ""));
    const y = Buffer.from(String(b ?? ""));
    return x.length === y.length && timingSafeEqual(x, y);
}

function sendJson(res, status, body) {
    res.writeHead(status, { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" });
    res.end(JSON.stringify(body));
}

async function readJson(req) {
    const chunks = [];
    let size = 0;
    for await (const chunk of req) {
        size += chunk.length;
        if (size > MAX_BODY) throw Object.assign(new Error("Request body too large"), { status: 413 });
        chunks.push(chunk);
    }
    const text = Buffer.concat(chunks).toString("utf8");
    return text ? JSON.parse(text) : {};
}

const MAX_COMBINE = 150;

export function validateCombineBranchName(branch) {
    if (!branch) return "Branch name is required.";
    if (branch.length > 100) return "Branch name must be at most 100 characters.";
    if (branch === "@") return "Branch name cannot be '@'.";
    if (branch.startsWith("-")) return "Branch name cannot start with '-'.";
    if (branch.startsWith("/") || branch.endsWith("/")) return "Branch name cannot start or end with '/'.";
    if (branch.endsWith(".")) return "Branch name cannot end with '.'.";
    if (branch.includes("..")) return "Branch name cannot contain '..'.";
    if (branch.includes("//")) return "Branch name cannot contain empty path components ('//').";
    if (branch.includes("@{")) return "Branch name cannot contain '@{'.";
    if (INVALID_BRANCH_CHARS_RE.test(branch)) {
        return "Branch name cannot contain spaces, ASCII control characters, or these characters: ~ ^ : ? * [ \\";
    }
    // Stricter than git: the branch is interpolated into the agent's shell commands.
    if (!SAFE_BRANCH_CHARS_RE.test(branch)) return "Branch name must start with a letter or digit and use only letters, digits, '.', '_', '-' or '/'.";
    if (branch.startsWith("dependabot/")) return "Branch name cannot be under dependabot/.";
    const parts = branch.split("/");
    const dotted = parts.find((part) => part.startsWith("."));
    if (dotted) return `Branch path component '${dotted}' cannot start with '.'.`;
    const locked = parts.find((part) => part.endsWith(".lock"));
    if (locked) return `Branch path component '${locked}' cannot end with '.lock'.`;
    return null;
}

function parseTarget(value, agents) {
    if (value === undefined || value === null || value === "" || value === "new") return "new";
    if (value === "main") return "main";
    if (typeof value === "string" && agents.isActive(value)) return value;
    throw Object.assign(new Error("That agent has finished or is no longer tracked — pick a new agent or this session."), { status: 409 });
}

function toNumbers(value) {
    const list = Array.isArray(value) ? value : [value];
    const nums = list.map(Number).filter((n) => Number.isInteger(n) && n > 0);
    return [...new Set(nums)];
}

export async function startInstanceServer({ instanceId, store, agents, onSendToSession, onCombine, onUiState }) {
    const token = randomBytes(24).toString("hex");
    const clients = new Set();
    let port = 0;

    const broadcast = (event, data) => {
        const payload = `event: ${event}\ndata: ${JSON.stringify(data)}\n\n`;
        for (const res of clients) res.write(payload);
    };
    const onChange = () => broadcast("state", store.snapshot());
    const onJob = (job) => broadcast("job", job);
    const onAgents = () => broadcast("agents", agents.list(store.repo));
    store.on("change", onChange);
    store.on("job", onJob);
    agents.on("change", onAgents);

    const heartbeat = setInterval(() => {
        for (const res of clients) res.write(": ping\n\n");
    }, 25_000);

    const server = createServer(async (req, res) => {
        try {
            // Loopback + Host check guards against DNS rebinding.
            const expectedHost = `127.0.0.1:${port}`;
            if (req.headers.host !== expectedHost) return sendJson(res, 421, { error: "Invalid host" });
            const url = new URL(req.url, `http://${expectedHost}`);
            const path = url.pathname;

            if (req.method === "GET" && (path === "/" || path === "/index.html")) {
                const html = (await readFile(join(UI_DIR, "index.html"), "utf8")).replace("__CANVAS_TOKEN__", token);
                res.writeHead(200, {
                    "Content-Type": "text/html; charset=utf-8",
                    "Cache-Control": "no-store",
                    "Content-Security-Policy":
                        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' https: data:; frame-src 'self' about:; connect-src 'self'",
                });
                return res.end(html);
            }
            if (req.method === "GET" && STATIC[path]) {
                res.writeHead(200, { "Content-Type": STATIC[path], "Cache-Control": "no-store" });
                return res.end(await readFile(join(UI_DIR, path.slice(1))));
            }

            // Everything below requires the per-instance token.
            const presented = path === "/events" ? url.searchParams.get("token") : req.headers["x-canvas-token"];
            if (!safeEqual(presented, token)) return sendJson(res, 403, { error: "Forbidden" });
            if (req.method === "POST") {
                const origin = req.headers.origin;
                if (origin && origin !== `http://${expectedHost}`) return sendJson(res, 403, { error: "Bad origin" });
            }

            if (req.method === "GET" && path === "/events") {
                res.writeHead(200, { "Content-Type": "text/event-stream", "Cache-Control": "no-store", Connection: "keep-alive" });
                res.write(`event: state\ndata: ${JSON.stringify(store.snapshot())}\n\n`);
                res.write(`event: agents\ndata: ${JSON.stringify(agents.list(store.repo))}\n\n`);
                for (const job of store.jobs.values()) if (job.status === "running") res.write(`event: job\ndata: ${JSON.stringify(job)}\n\n`);
                clients.add(res);
                req.on("close", () => clients.delete(res));
                return;
            }
            if (req.method === "GET" && path === "/api/state") return sendJson(res, 200, store.snapshot());

            const prMatch = path.match(/^\/api\/pr\/(\d+)$/);
            if (req.method === "GET" && prMatch) {
                const details = await store.getDetails(Number(prMatch[1]), { force: url.searchParams.get("force") === "1" });
                return sendJson(res, 200, details);
            }
            const diffMatch = path.match(/^\/api\/pr\/(\d+)\/diff$/);
            if (req.method === "GET" && diffMatch) {
                const diff = await store.getDiff(Number(diffMatch[1]), { force: url.searchParams.get("force") === "1" });
                return sendJson(res, 200, diff);
            }

            if (req.method !== "POST") return sendJson(res, 404, { error: "Not found" });
            const body = await readJson(req);

            if (path === "/api/refresh") {
                store.refresh();
                return sendJson(res, 202, { ok: true });
            }
            if (path === "/api/action") {
                if (!ACTION_TYPES.has(body.type)) return sendJson(res, 400, { error: `Unknown action ${body.type}` });
                const numbers = toNumbers(body.numbers);
                if (!numbers.length) return sendJson(res, 400, { error: "No pull requests selected" });
                if (!store.repoInfo?.canWrite && body.type !== "dependabot") {
                    return sendJson(res, 403, { error: "You need write access to this repository for that action." });
                }
                const options = body.options ?? {};
                if ((body.type === "merge" || body.type === "approve-merge") && !store.repoInfo?.mergeMethods.includes(options.method)) {
                    return sendJson(res, 400, { error: `Merge method '${options.method}' is not allowed in this repository.` });
                }
                const job = store.startJob(body.type, numbers, options);
                return sendJson(res, 202, { jobId: job.id });
            }
            if (path === "/api/send") {
                const numbers = toNumbers(body.numbers);
                if (!numbers.length) return sendJson(res, 400, { error: "No pull requests selected" });
                const target = parseTarget(body.target, agents);
                const result = await onSendToSession({ numbers, note: typeof body.note === "string" ? body.note.slice(0, 4000) : "", target });
                return sendJson(res, 200, { ok: true, ...result });
            }
            if (path === "/api/review/clear") {
                await store.clearReview(toNumbers(body.numbers));
                return sendJson(res, 200, { ok: true });
            }
            if (path === "/api/combine") {
                const numbers = toNumbers(body.numbers).filter((n) => store.prs.has(n));
                if (numbers.length < 2) return sendJson(res, 400, { error: "Select at least two open Dependabot PRs to combine." });
                if (numbers.length > MAX_COMBINE) return sendJson(res, 400, { error: `Combine at most ${MAX_COMBINE} PRs at a time.` });
                const branch = typeof body.branch === "string" ? body.branch.trim() : "";
                const branchError = validateCombineBranchName(branch);
                if (branchError) return sendJson(res, 400, { error: branchError });
                const title = typeof body.title === "string" ? body.title.trim().replace(/\s+/g, " ") : "";
                if (!title || title.length > 200) return sendJson(res, 400, { error: "Title is required (max 200 characters)." });
                const target = parseTarget(body.target, agents);
                const request = await store.createCombineRequest({
                    numbers,
                    branch,
                    title,
                    base: store.repoInfo?.defaultBranch ?? "main",
                    draft: body.draft === true,
                    fixBreakages: body.fixBreakages === true,
                });
                let result;
                try {
                    result = await onCombine({ request, note: typeof body.note === "string" ? body.note.slice(0, 4000) : "", target });
                } catch (e) {
                    await store.clearCombined(request.id);
                    throw e;
                }
                return sendJson(res, 200, { ok: true, requestId: request.id, ...result });
            }
            if (path === "/api/combined/clear") {
                if (typeof body.id !== "string" || !store.combined[body.id]) return sendJson(res, 404, { error: "Unknown combine request" });
                await store.clearCombined(body.id);
                return sendJson(res, 200, { ok: true });
            }
            if (path === "/api/agents/refresh") {
                await agents.poll();
                return sendJson(res, 200, { ok: true });
            }
            if (path.startsWith("/api/agents/")) {
                const agentId = typeof body.agentId === "string" ? body.agentId : "";
                const rec = agents.get(agentId);
                if (!rec || rec.repo?.toLowerCase() !== store.repo.toLowerCase()) return sendJson(res, 404, { error: "Unknown agent" });
                if (path === "/api/agents/message") {
                    const message = typeof body.message === "string" ? body.message.trim().slice(0, 8000) : "";
                    if (!message) return sendJson(res, 400, { error: "Message is required." });
                    if (!agents.isActive(agentId)) return sendJson(res, 409, { error: "That agent has finished; start a new one instead." });
                    await agents.message(agentId, { prompt: `Message from the user via the Dependabot triage canvas:\n\n${message}`, kind: "message" });
                    return sendJson(res, 200, { ok: true });
                }
                if (path === "/api/agents/cancel") return sendJson(res, 200, { ok: true, cancelled: await agents.cancel(agentId) });
                if (path === "/api/agents/dismiss") {
                    await agents.dismiss(agentId);
                    return sendJson(res, 200, { ok: true });
                }
            }
            if (path === "/api/ui-state") {
                onUiState(body);
                return sendJson(res, 200, { ok: true });
            }
            return sendJson(res, 404, { error: "Not found" });
        } catch (e) {
            if (!res.headersSent) sendJson(res, e.status ?? 500, { error: e.message });
            else res.end();
        }
    });

    await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
    port = server.address().port;

    return {
        instanceId,
        url: `http://127.0.0.1:${port}/`,
        sendCommand: (command) => broadcast("command", command),
        async close() {
            clearInterval(heartbeat);
            store.off("change", onChange);
            store.off("job", onJob);
            agents.off("change", onAgents);
            for (const res of clients) res.end();
            clients.clear();
            await new Promise((resolve) => server.close(() => resolve()));
        },
    };
}
