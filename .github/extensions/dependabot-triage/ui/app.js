// Dependabot triage canvas client. Vanilla JS; all DOM is built via h() so
// untrusted strings from GitHub are always inserted as text.

const token = document.querySelector('meta[name="canvas-token"]').content;

const STATUS = {
    ready: { label: "Ready", order: 0 },
    "needs-review": { label: "Needs review", order: 1 },
    failing: { label: "Checks failing", order: 2 },
    conflicts: { label: "Conflicts", order: 3 },
    pending: { label: "Checks pending", order: 4 },
    behind: { label: "Behind base", order: 5 },
    draft: { label: "Draft", order: 6 },
    unknown: { label: "Computing…", order: 7 },
};
const SEVERITY_ORDER = { critical: 0, high: 1, medium: 2, moderate: 2, low: 3, unknown: 4 };
const JOB_LABELS = {
    approve: "Approving",
    merge: "Merging",
    "approve-merge": "Approving & merging",
    "disable-auto": "Disabling auto-merge",
    dependabot: "Commenting",
    close: "Closing",
};

const ui = {
    state: null,
    tab: "prs",
    filters: { q: "", status: "", ecosystem: "", updateType: "", securityOnly: false },
    groupBy: "dependency",
    selected: null,
    checked: new Set(),
    collapsed: new Set(),
    details: new Map(),
    detailsLoading: new Set(),
    detailsError: new Map(),
    diffs: new Map(),
    diffLoading: new Set(),
    diffError: new Map(),
    diffOpen: new Map(),
    jobs: new Map(),
    dismissedJobs: new Set(),
    alertsUnaddressedOnly: false,
    hotspotLimit: 60,
    mergeMethod: null,
    agents: [],
    lastTarget: "new",
};

const ACTIVE_AGENT = new Set(["running", "idle"]);
const AGENT_STATUS = {
    running: { label: "running", cls: "b-pending" },
    idle: { label: "idle — waiting", cls: "b-combined" },
    completed: { label: "completed", cls: "b-safe" },
    failed: { label: "failed", cls: "b-blocked" },
    cancelled: { label: "cancelled", cls: "b-outline" },
    gone: { label: "no longer tracked", cls: "b-outline" },
};

function agentsFor(number) {
    return ui.agents.filter((a) => a.status === "running" && a.numbers.includes(number));
}

// ---------------------------------------------------------------------------
// Utilities

function h(tag, attrs, ...children) {
    const el = document.createElement(tag);
    if (attrs) {
        for (const [k, v] of Object.entries(attrs)) {
            if (v === null || v === undefined || v === false) continue;
            if (k === "class") el.className = v;
            else if (k.startsWith("on") && typeof v === "function") el.addEventListener(k.slice(2), v);
            else if (k === "checked" || k === "value" || k === "indeterminate" || k === "selected") el[k] = v;
            else if (v === true) el.setAttribute(k, "");
            else el.setAttribute(k, String(v));
        }
    }
    for (const c of children.flat(Infinity)) {
        if (c === null || c === undefined || c === false) continue;
        el.append(c instanceof Node ? c : String(c));
    }
    return el;
}

const $ = (id) => document.getElementById(id);

async function api(path, { method = "GET", body } = {}) {
    const res = await fetch(path, {
        method,
        headers: { "Content-Type": "application/json", "X-Canvas-Token": token },
        body: body === undefined ? undefined : JSON.stringify(body),
    });
    const json = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(json.error || `${res.status} ${res.statusText}`);
    return json;
}

function ago(iso) {
    if (!iso) return "";
    const s = Math.max(0, (Date.now() - Date.parse(iso)) / 1000);
    if (s < 60) return "just now";
    if (s < 3600) return `${Math.floor(s / 60)}m ago`;
    if (s < 86400) return `${Math.floor(s / 3600)}h ago`;
    return `${Math.floor(s / 86400)}d ago`;
}

function prMap() {
    return new Map((ui.state?.prs ?? []).map((p) => [p.number, p]));
}

function depSummary(pr) {
    if (pr.updates.length === 1) {
        const u = pr.updates[0];
        return { name: u.name, versions: `${u.from ?? "?"} → ${u.to ?? "?"}` };
    }
    if (pr.updates.length > 1) {
        const name = pr.groupName ? `${pr.groupName} group` : pr.title.replace(/^bump\s+/i, "");
        return { name, versions: `${pr.updates.length} updates` };
    }
    return { name: pr.title, versions: "" };
}

function statusDot(status) {
    return h("span", { class: `status-dot s-${status}`, title: STATUS[status]?.label ?? status });
}

function badge(text, cls, title) {
    return h("span", { class: `badge ${cls ?? ""}`, title }, text);
}

function prChip(number, onClick) {
    const pr = prMap().get(number);
    return h(
        "button",
        { class: "chip", type: "button", title: pr ? `${pr.title} — ${STATUS[pr.status]?.label}` : `#${number}`, onclick: onClick ?? (() => focusPr(number)) },
        pr ? statusDot(pr.status) : null,
        `#${number}`,
    );
}

function toast(title, { kind = "", body, timeout = 6000 } = {}) {
    const el = h(
        "div",
        { class: `toast ${kind}` },
        h("div", { class: "title" }, h("span", null, title), h("button", { class: "close", type: "button", onclick: () => el.remove(), "aria-label": "Dismiss" }, "✕")),
        body ? h("div", null, body) : null,
    );
    $("toasts").prepend(el);
    if (timeout) setTimeout(() => el.remove(), timeout);
}

// ---------------------------------------------------------------------------
// Filtering & grouping

function matches(pr) {
    const f = ui.filters;
    if (f.q) {
        const q = f.q.toLowerCase().trim();
        const hay = [pr.title, `#${pr.number}`, pr.directory, pr.ecosystem, ...pr.updates.map((u) => u.name)].join(" ").toLowerCase();
        if (!hay.includes(q)) return false;
    }
    if (f.status && pr.status !== f.status) return false;
    if (f.ecosystem && pr.ecosystem !== f.ecosystem) return false;
    if (f.updateType && pr.highestUpdateType !== f.updateType) return false;
    if (f.securityOnly && !pr.security.fixes.length && !pr.security.related.length) return false;
    return true;
}

function visiblePrs() {
    return (ui.state?.prs ?? []).filter(matches);
}

function groupKey(pr) {
    switch (ui.groupBy) {
        case "dependency":
            if (pr.updates.length === 1) {
                const u = pr.updates[0];
                return { key: `${pr.ecosystem}:${u.name.toLowerCase()}@${u.to}`, label: `${u.name} → ${u.to ?? "?"}`, sub: pr.ecosystem };
            }
            return { key: `group:${pr.ecosystem}:${pr.groupName ?? pr.title}`, label: pr.groupName ? `${pr.groupName} group` : pr.title, sub: pr.ecosystem };
        case "directory":
            return { key: `${pr.ecosystem}:${pr.directory}`, label: pr.directory, sub: pr.ecosystem };
        case "ecosystem":
            return { key: pr.ecosystem, label: pr.ecosystem, sub: "" };
        case "status":
            return { key: pr.status, label: STATUS[pr.status]?.label ?? pr.status, sub: "", order: STATUS[pr.status]?.order ?? 99 };
        default:
            return { key: "all", label: "All", sub: "" };
    }
}

function groupPrs(prs) {
    const groups = new Map();
    for (const pr of prs) {
        const g = groupKey(pr);
        if (!groups.has(g.key)) groups.set(g.key, { ...g, prs: [] });
        groups.get(g.key).prs.push(pr);
    }
    const list = [...groups.values()];
    for (const g of list) g.prs.sort((a, b) => b.number - a.number);
    if (ui.groupBy === "status") list.sort((a, b) => a.order - b.order);
    else list.sort((a, b) => b.prs.length - a.prs.length || a.label.localeCompare(b.label));
    return list;
}

// ---------------------------------------------------------------------------
// Rendering: header

function renderHeader() {
    const s = ui.state;
    const link = $("repo-link");
    link.textContent = s ? `Dependabot · ${s.repo}` : "Dependabot triage";
    if (s?.repoInfo?.url) link.href = `${s.repoInfo.url}/pulls/app%2Fdependabot`;

    const stats = $("stats");
    stats.replaceChildren();
    if (s) {
        const c = s.counts;
        const chip = (label, count, status, cls) =>
            h(
                "button",
                {
                    class: `stat ${ui.filters.status === status && status ? "active" : ""}`,
                    type: "button",
                    title: status ? `Filter: ${label}` : label,
                    onclick: () => {
                        if (status === "__security") {
                            setFilters({ securityOnly: !ui.filters.securityOnly, status: "" });
                        } else setFilters({ status: ui.filters.status === status ? "" : status });
                        switchTab("prs");
                    },
                },
                status && status !== "__security" ? statusDot(status) : cls ? h("span", { class: cls }, "🛡") : null,
                h("b", null, String(count ?? 0)),
                label,
            );
        stats.append(
            chip("open", c.open, ""),
            chip("ready", c.ready, "ready"),
            chip("need review", c["needs-review"], "needs-review"),
            chip("failing", c.failing, "failing"),
            chip("conflicts", c.conflicts, "conflicts"),
            chip("pending", c.pending, "pending"),
            chip("fix alerts", c.security, "__security", "c-bad"),
        );
    }

    const loading = s?.loading;
    let status = "";
    if (loading?.list) status = `Loading PRs${loading.progress?.total ? ` ${loading.progress.loaded}/${loading.progress.total}` : "…"}`;
    else if (loading?.mergeability) status = "Checking mergeability…";
    else if (s?.lastRefreshed) status = `Updated ${ago(s.lastRefreshed)}`;
    $("refresh-status").textContent = status;
    $("refresh-btn").disabled = !!(loading?.list || loading?.mergeability);

    const banner = $("banner");
    const prs = prMap();
    const mergedCombines = (s?.combined ?? [])
        .filter((c) => c.status === "opened" && c.prState === "MERGED")
        .map((c) => ({ rec: c, open: c.included.filter((n) => prs.has(n)) }))
        .filter((m) => m.open.length);
    if (s?.error) {
        banner.hidden = false;
        banner.className = "banner";
        banner.textContent = `GitHub error: ${s.error}`;
    } else if (s?.repoInfo && !s.repoInfo.canWrite) {
        banner.hidden = false;
        banner.className = "banner warn";
        banner.textContent = `You have ${s.repoInfo.viewerPermission} access to ${s.repo}; approving, merging and closing are disabled. Dependabot commands may still work.`;
    } else if (mergedCombines.length) {
        banner.hidden = false;
        banner.className = "banner info";
        banner.replaceChildren(
            ...mergedCombines.map(({ rec, open }) =>
                h(
                    "div",
                    { class: "banner-row" },
                    h("span", null, `Combined PR #${rec.prNumber} is merged, but ${open.length} PR(s) it supersedes are still open. Dependabot only closes them on its next scheduled run.`),
                    h("button", { class: "btn btn-small btn-danger", type: "button", onclick: () => confirmClose(open, { supersededBy: rec }) }, `Close ${open.length} superseded…`),
                ),
            ),
        );
    } else banner.hidden = true;

    $("tab-count-prs").textContent = s ? String(s.counts.open) : "";
    $("tab-count-alerts").textContent = s ? (s.alertsError ? "!" : String(s.counts.alerts)) : "";
    $("tab-count-overlaps").textContent = s ? String(s.dependencyClusters.length + s.fileHotspots.length) : "";
    const runningAgents = ui.agents.filter((a) => a.status === "running").length;
    $("tab-count-agents").textContent = runningAgents ? `${runningAgents} running` : ui.agents.length ? String(ui.agents.length) : "";

    const ecoSelect = $("f-ecosystem");
    const ecos = [...new Set((s?.prs ?? []).map((p) => p.ecosystem))].sort();
    const current = [...ecoSelect.options].slice(1).map((o) => o.value).join(",");
    if (current !== ecos.join(",")) {
        ecoSelect.replaceChildren(h("option", { value: "" }, "All ecosystems"), ...ecos.map((e) => h("option", { value: e }, e)));
        ecoSelect.value = ui.filters.ecosystem;
    }
}

// ---------------------------------------------------------------------------
// Rendering: list

function renderList() {
    const list = $("list");
    const scroll = list.scrollTop;
    list.replaceChildren();
    const s = ui.state;
    if (!s) {
        list.append(h("div", { class: "loading-row" }, "Connecting…"));
        return;
    }
    const prs = visiblePrs();
    if (!prs.length) {
        list.append(h("div", { class: "empty" }, s.loading.list ? "Loading Dependabot pull requests…" : s.prs.length ? "No PRs match the current filters." : "No open Dependabot pull requests 🎉"));
        return;
    }
    const groups = groupPrs(prs);
    for (const g of groups) {
        if (ui.groupBy !== "none") list.append(renderGroupHeader(g));
        if (ui.groupBy !== "none" && ui.collapsed.has(g.key)) continue;
        for (const pr of g.prs) list.append(renderRow(pr));
    }
    if (s.loading.list) list.append(h("div", { class: "loading-row" }, "Loading more…"));
    list.scrollTop = scroll;
}

function renderGroupHeader(g) {
    const nums = g.prs.map((p) => p.number);
    const checkedCount = nums.filter((n) => ui.checked.has(n)).length;
    const statuses = {};
    for (const p of g.prs) statuses[p.status] = (statuses[p.status] ?? 0) + 1;
    const security = g.prs.filter((p) => p.security.fixes.length).length;
    const highest = ["major", "minor", "patch"].find((t) => g.prs.some((p) => p.highestUpdateType === t));
    const collapsed = ui.collapsed.has(g.key);
    return h(
        "div",
        {
            class: "group-header",
            onclick: (e) => {
                if (e.target.tagName === "INPUT") return;
                if (collapsed) ui.collapsed.delete(g.key);
                else ui.collapsed.add(g.key);
                renderList();
            },
        },
        h("input", {
            type: "checkbox",
            checked: checkedCount === nums.length,
            indeterminate: checkedCount > 0 && checkedCount < nums.length,
            title: "Select all in group",
            onchange: (e) => {
                for (const n of nums) e.target.checked ? ui.checked.add(n) : ui.checked.delete(n);
                renderAll();
            },
        }),
        h("span", { class: "caret" }, collapsed ? "▸" : "▾"),
        h("span", { class: "name", title: g.label }, g.label),
        g.sub ? h("span", { class: "muted" }, g.sub) : null,
        h("span", { class: "spacer" }),
        security ? badge(`🛡 ${security}`, "b-security", "PRs fixing security alerts") : null,
        highest ? badge(highest, `b-${highest}`) : null,
        ...Object.entries(statuses)
            .sort((a, b) => (STATUS[a[0]]?.order ?? 9) - (STATUS[b[0]]?.order ?? 9))
            .map(([st, n]) => h("span", { class: "badge b-outline", title: STATUS[st]?.label }, statusDot(st), String(n))),
        nums.length >= 2 && ui.groupBy !== "status"
            ? h(
                  "button",
                  {
                      class: "btn btn-small btn-accent",
                      type: "button",
                      title: `Ask an agent to combine these ${nums.length} PRs into a single PR`,
                      onclick: (e) => {
                          e.stopPropagation();
                          confirmCombine(nums);
                      },
                  },
                  "⊕ Combine",
              )
            : null,
    );
}

function renderRow(pr) {
    const d = depSummary(pr);
    const overlapCount = pr.overlaps.sharedFiles.length;
    return h(
        "div",
        {
            class: `row ${ui.selected === pr.number ? "selected" : ""}`,
            role: "listitem",
            "data-number": pr.number,
            onclick: (e) => {
                if (e.target.tagName === "INPUT") return;
                focusPr(pr.number, { scroll: false });
            },
        },
        h("input", {
            type: "checkbox",
            checked: ui.checked.has(pr.number),
            "aria-label": `Select #${pr.number}`,
            onchange: (e) => {
                e.target.checked ? ui.checked.add(pr.number) : ui.checked.delete(pr.number);
                renderAll();
            },
        }),
        statusDot(pr.status),
        h(
            "div",
            { class: "main-text" },
            h("div", { class: "line1" }, h("span", { class: "num" }, `#${pr.number}`), h("span", { class: "dep", title: pr.title }, d.name), h("span", { class: "muted small" }, d.versions)),
            h("div", { class: "line2" }, `${pr.ecosystem} · ${pr.directory} · ${STATUS[pr.status]?.label ?? pr.status} · ${ago(pr.createdAt)}`),
        ),
        h(
            "div",
            { class: "badges" },
            pr.security.fixes.length ? badge("🛡", "b-security", `Fixes ${pr.security.fixes.length} security alert(s)`) : pr.security.related.length ? badge("🛡", "b-related", "Related to a security alert") : null,
            pr.agentReview ? badge(pr.agentReview.stale ? `${pr.agentReview.verdict}*` : pr.agentReview.verdict, `b-${pr.agentReview.verdict}`, pr.agentReview.stale ? "Agent review (PR changed since)" : "Agent review verdict") : null,
            pr.combine ? badge(pr.combine.prNumber ? `⊕#${pr.combine.prNumber}` : "⊕…", "b-combined", pr.combine.prNumber ? `Superseded by combined PR #${pr.combine.prNumber} (${(pr.combine.prState ?? "").toLowerCase()})` : "Combine requested — waiting for the agent") : null,
            agentsFor(pr.number).length ? badge("⚙", "b-pending", `Agent working on this PR: ${agentsFor(pr.number).map((a) => `${a.name} (${a.status})`).join(", ")}`) : null,
            pr.reviewDecision === "APPROVED" ? badge("✓", "b-approved", "Approved") : null,
            pr.autoMerge ? badge("auto", "b-auto", "Auto-merge enabled") : null,
            overlapCount ? badge(`⧉${overlapCount}`, "b-overlap", `Shares files with ${overlapCount} other PR(s)`) : null,
            badge(pr.highestUpdateType, `b-${pr.highestUpdateType}`),
        ),
    );
}

// ---------------------------------------------------------------------------
// Rendering: detail

let lastDetailSignature = null;

function detailSignature(pr) {
    if (!pr) return `none:${ui.groupBy}:${ui.state?.dependencyClusters?.length}`;
    const d = ui.details.get(pr.number);
    const alerts = (ui.state.alerts ?? []).filter((a) => pr.security.fixes.includes(a.number) || pr.security.related.includes(a.number));
    return JSON.stringify([
        pr,
        d?.fetchedAt ?? null,
        ui.detailsError.get(pr.number) ?? null,
        alerts.map((a) => a.number),
        ui.state.alertsError,
        ui.state.loading.alerts,
        ui.state.repoInfo?.canWrite,
        pr.overlaps.sameDependency.map((n) => prMap().get(n)?.status),
        ui.diffs.get(pr.number)?.fetchedAt ?? null,
        ui.diffError.get(pr.number) ?? null,
        ui.diffLoading.has(pr.number),
        pr.combine ? combineRecord(pr.combine.id) : null,
        agentsFor(pr.number).map((a) => [a.agentId, a.status, a.latestIntent]),
    ]);
}

let lastDetailNumber = null;

function renderDetail({ force = false } = {}) {
    const el = $("detail");
    const pr = prMap().get(ui.selected);
    const signature = detailSignature(pr);
    if (!force && signature === lastDetailSignature) return;
    lastDetailSignature = signature;
    const prevScroll = el.scrollTop;
    const samePr = lastDetailNumber === ui.selected;
    lastDetailNumber = ui.selected;
    el.replaceChildren();
    renderDetailInto(el, pr);
    if (samePr) el.scrollTop = prevScroll;
}

function renderDetailInto(el, pr) {
    if (!pr) {
        el.append(renderOverviewHelp());
        return;
    }
    const details = ui.details.get(pr.number);
    if (!details && !ui.detailsLoading.has(pr.number) && !ui.detailsError.has(pr.number)) loadDetails(pr.number);
    const cachedDiff = ui.diffs.get(pr.number);
    const diffStale = cachedDiff && pr.headOid && cachedDiff.headOid && cachedDiff.headOid !== pr.headOid;
    if ((!cachedDiff || diffStale) && !ui.diffLoading.has(pr.number) && !ui.diffError.has(pr.number)) loadDiff(pr.number);
    const canWrite = !!ui.state.repoInfo?.canWrite;

    el.append(
        h("button", { class: "btn btn-small back-btn", type: "button", onclick: () => document.body.classList.remove("show-detail") }, "← Back to list"),
        h("h2", null, h("a", { href: pr.url, target: "_blank", rel: "noopener" }, `#${pr.number} ${pr.title}`)),
        h(
            "div",
            { class: "meta" },
            badge(STATUS[pr.status]?.label ?? pr.status, `b-${pr.status}`),
            badge(`checks: ${(pr.checks ?? "none").toLowerCase()}`, "b-outline"),
            badge(`${pr.mergeable.toLowerCase()} · ${pr.mergeStateStatus.toLowerCase()}`, "b-outline", "mergeable · merge state"),
            pr.reviewDecision ? badge(pr.reviewDecision.toLowerCase().replace("_", " "), pr.reviewDecision === "APPROVED" ? "b-approved" : "b-outline") : null,
            badge(pr.highestUpdateType, `b-${pr.highestUpdateType}`),
            badge(pr.ecosystem, "b-outline"),
            badge(pr.directory, "b-outline"),
            pr.autoMerge ? badge(`auto-merge (${pr.autoMerge.method?.toLowerCase()})`, "b-auto") : null,
            pr.isDraft ? badge("draft", "b-outline") : null,
            ...pr.labels.map((l) => badge(l.name, "b-outline")),
            h("span", { class: "muted small" }, `opened ${ago(pr.createdAt)} · +${pr.additions} −${pr.deletions}`),
        ),
        renderActions(pr, canWrite),
    );

    if (pr.combine) el.append(renderCombineCallout(pr, canWrite));

    for (const a of agentsFor(pr.number)) {
        el.append(
            h(
                "div",
                { class: "callout agent" },
                h("div", null, badge("⚙ agent", "b-pending"), " ", h("b", null, a.name), h("span", { class: "muted" }, ` · ${AGENT_STATUS[a.status]?.label ?? a.status} · ${ago(a.lastPromptAt)}`), " ", h("button", { class: "btn btn-small", type: "button", onclick: () => switchTab("agents") }, "View agents")),
                a.status === "running" && a.latestIntent ? h("p", { class: "muted small" }, a.latestIntent) : null,
            ),
        );
    }

    if (pr.agentReview) {
        const r = pr.agentReview;
        el.append(
            h("h3", null, "Agent review"),
            h(
                "div",
                { class: `callout ${r.verdict}` },
                h("div", null, badge(r.verdict, `b-${r.verdict}`), " ", h("span", { class: "muted" }, `${r.reviewer ?? "agent"} · ${ago(r.recordedAt)}`), r.stale ? h("span", { class: "c-bad" }, " · PR has new commits since this review") : null, " ", h("button", { class: "btn btn-small", type: "button", onclick: () => api("/api/review/clear", { method: "POST", body: { numbers: [pr.number] } }) }, "Clear")),
                h("p", null, r.summary),
            ),
        );
    }

    el.append(h("h3", null, "Updates"));
    el.append(
        h(
            "table",
            { class: "grid" },
            h("thead", null, h("tr", null, h("th", null, "Dependency"), h("th", null, "From"), h("th", null, "To"), h("th", null, "Type"), h("th", null, "Dependency type"))),
            h(
                "tbody",
                null,
                pr.updates.length
                    ? pr.updates.map((u) => h("tr", null, h("td", null, h("code", null, u.name)), h("td", null, u.from ?? "?"), h("td", null, u.to ?? "?"), h("td", null, badge(u.type, `b-${u.type}`)), h("td", { class: "muted" }, u.dependencyType ?? "")))
                    : h("tr", null, h("td", { colspan: 5, class: "muted" }, "Could not parse dependency metadata from this PR.")),
            ),
        ),
    );

    el.append(renderSecurity(pr));
    el.append(renderOverlaps(pr));
    el.append(renderChecks(pr, details));
    if (details?.comments?.length) {
        el.append(h("h3", null, "Recent comments"));
        for (const c of details.comments.slice(-5).reverse()) {
            el.append(h("div", { class: "comment" }, h("div", { class: "muted" }, h("b", null, c.author), ` · ${ago(c.createdAt)} · `, h("a", { href: c.url, target: "_blank", rel: "noopener" }, "view")), h("div", { class: "body" }, c.body)));
        }
    }
    if (details?.bodyHTML) el.append(renderReleaseNotes(details.bodyHTML));
    el.append(renderFiles(pr, details));
}

function renderOverviewHelp() {
    const s = ui.state;
    return h(
        "div",
        { class: "empty" },
        h("p", null, "Select a pull request to see checks, related security alerts and overlaps."),
        s && s.counts.ready ? h("p", null, `${s.counts.ready} PR(s) are ready. Use `, h("b", null, "Select ready"), " to bulk approve & merge.") : null,
        s && s.dependencyClusters.length
            ? h("p", null, `${s.dependencyClusters.length} dependencies are bumped in more than one PR${ui.groupBy === "dependency" ? " — tick a group header to act on all of them at once." : " — group by dependency to review them together."}`)
            : null,
    );
}

function renderActions(pr, canWrite) {
    const commands = ui.state.dependabotCommands;
    const cmdSelect = h("select", { title: "Dependabot command" }, Object.entries(commands).map(([k, v]) => h("option", { value: k }, v.label)));
    const depSelect = h("select", { title: "Dependency", hidden: true }, pr.updates.map((u) => h("option", { value: u.name }, u.name)));
    cmdSelect.addEventListener("change", () => (depSelect.hidden = !commands[cmdSelect.value]?.needsDependency));
    const approved = pr.reviewDecision === "APPROVED";

    return h(
        "div",
        { class: "actions" },
        h("button", { class: "btn", type: "button", disabled: !canWrite || approved, title: approved ? "Already approved" : "Submit an approving review", onclick: () => confirmApprove([pr.number]) }, approved ? "✓ Approved" : "✓ Approve"),
        h("button", { class: "btn btn-primary", type: "button", disabled: !canWrite, onclick: () => confirmMerge([pr.number], { approve: !approved }) }, approved ? "Merge…" : "Approve & merge…"),
        pr.autoMerge ? h("button", { class: "btn", type: "button", disabled: !canWrite, onclick: () => runAction("disable-auto", [pr.number]) }, "Disable auto-merge") : null,
        h("span", { class: "sep" }),
        h("button", { class: "btn btn-accent", type: "button", onclick: () => confirmSend([pr.number]) }, "✦ Review with agent…"),
        pr.overlaps.sameDependency.length ? h("button", { class: "btn btn-accent", type: "button", title: "Send this PR and every PR bumping the same dependency", onclick: () => confirmSend([pr.number, ...pr.overlaps.sameDependency]) }, `✦ Review all ${pr.overlaps.sameDependency.length + 1}`) : null,
        pr.overlaps.sameDependency.length ? h("button", { class: "btn btn-accent", type: "button", title: "Ask an agent to combine this PR and every PR bumping the same dependency into one PR", onclick: () => confirmCombine([pr.number, ...pr.overlaps.sameDependency]) }, `⊕ Combine all ${pr.overlaps.sameDependency.length + 1}…`) : null,
        h("span", { class: "sep" }),
        cmdSelect,
        depSelect,
        h("button", { class: "btn", type: "button", onclick: () => confirmDependabot([pr.number], cmdSelect.value, depSelect.hidden ? undefined : depSelect.value) }, "Run"),
        h("span", { class: "sep" }),
        h("button", { class: "btn btn-danger", type: "button", disabled: !canWrite, onclick: () => confirmClose([pr.number]) }, "Close…"),
        h("button", { class: "btn btn-small", type: "button", title: "Re-fetch this PR", onclick: () => { loadDetails(pr.number, true); loadDiff(pr.number, true); } }, "⟳"),
    );
}

function renderSecurity(pr) {
    const alerts = new Map((ui.state.alerts ?? []).map((a) => [a.number, a]));
    const fixes = pr.security.fixes.map((n) => alerts.get(n)).filter(Boolean);
    const related = pr.security.related.map((n) => alerts.get(n)).filter(Boolean);
    const wrap = h("div");
    wrap.append(h("h3", null, "Security", fixes.length ? badge(`fixes ${fixes.length}`, "b-security") : null));
    if (ui.state.alertsError) {
        wrap.append(h("div", { class: "hint" }, ui.state.alertsError));
        return wrap;
    }
    if (!fixes.length && !related.length) {
        wrap.append(h("div", { class: "hint" }, ui.state.loading.alerts ? "Loading alerts…" : "No open Dependabot alerts for these dependencies."));
        return wrap;
    }
    const card = (a, fixesIt) =>
        h(
            "div",
            { class: `callout ${fixesIt ? "blocked" : "warn"}`, style: "margin-bottom:6px" },
            h("div", null, badge(a.severity, `b-${a.severity}`), " ", h("a", { href: a.url, target: "_blank", rel: "noopener" }, a.ghsa ?? `Alert #${a.number}`), a.cve ? h("span", { class: "muted" }, ` · ${a.cve}`) : null),
            h("p", null, a.summary),
            h("div", { class: "muted small" }, `${a.package} ${a.vulnerableRange ?? ""} → patched in ${a.patched ?? "n/a"} · ${a.manifestPath} (${a.relationship ?? "?"})`),
        );
    for (const a of fixes) wrap.append(card(a, true));
    if (related.length) {
        wrap.append(h("div", { class: "hint" }, "Same package, but not confirmed fixed by this PR (different manifest or version below the patched release):"));
        for (const a of related) wrap.append(card(a, false));
    }
    return wrap;
}

function renderOverlaps(pr) {
    const wrap = h("div");
    const same = pr.overlaps.sameDependency;
    const shared = pr.overlaps.sharedFiles;
    wrap.append(h("h3", null, "Overlaps", same.length || shared.length ? badge(`${new Set([...same, ...shared.map((s) => s.number)]).size} PRs`, "b-overlap") : null));
    if (!same.length && !shared.length) {
        wrap.append(h("div", { class: "hint" }, "No other open Dependabot PR touches the same dependency or files."));
        return wrap;
    }
    if (same.length) {
        const all = [pr.number, ...same];
        wrap.append(
            h("div", { class: "hint" }, "Same dependency also bumped in:"),
            h("div", { class: "chips" }, same.map((n) => prChip(n)), h("button", { class: "btn btn-small", type: "button", onclick: () => selectNumbers(all, true) }, `Select all ${all.length}`)),
        );
    }
    if (shared.length) {
        const byFiles = new Map();
        for (const o of shared) {
            const key = o.files.sort().join("\n");
            if (!byFiles.has(key)) byFiles.set(key, { files: o.files, numbers: [] });
            byFiles.get(key).numbers.push(o.number);
        }
        wrap.append(h("div", { class: "hint", style: "margin-top:8px" }, "Touches the same files as (merging one will require the others to rebase):"));
        for (const g of byFiles.values()) {
            wrap.append(
                h(
                    "div",
                    { style: "margin-bottom:6px" },
                    h("div", { class: "small mono muted" }, g.files.slice(0, 4).join(", "), g.files.length > 4 ? ` +${g.files.length - 4} more` : ""),
                    h("div", { class: "chips" }, g.numbers.map((n) => prChip(n))),
                ),
            );
        }
    }
    return wrap;
}

function checkIcon(state) {
    const s = (state ?? "").toUpperCase();
    if (s === "SUCCESS") return h("span", { class: "icon c-ok" }, "✓");
    if (["FAILURE", "ERROR", "TIMED_OUT", "ACTION_REQUIRED", "STARTUP_FAILURE", "CANCELLED"].includes(s)) return h("span", { class: "icon c-bad" }, "✗");
    if (["SKIPPED", "NEUTRAL", "STALE"].includes(s)) return h("span", { class: "icon c-skip" }, "–");
    return h("span", { class: "icon c-wait" }, "●");
}

function checkRank(state) {
    const s = (state ?? "").toUpperCase();
    if (["FAILURE", "ERROR", "TIMED_OUT", "ACTION_REQUIRED", "STARTUP_FAILURE", "CANCELLED"].includes(s)) return 0;
    if (s === "SUCCESS") return 2;
    if (["SKIPPED", "NEUTRAL", "STALE"].includes(s)) return 3;
    return 1;
}

function renderChecks(pr, details) {
    const wrap = h("div");
    wrap.append(h("h3", null, "Checks", badge((pr.checks ?? "none").toLowerCase(), pr.checks === "SUCCESS" ? "b-ready" : pr.checks === "FAILURE" || pr.checks === "ERROR" ? "b-failing" : "b-pending")));
    if (ui.detailsError.has(pr.number)) {
        wrap.append(h("div", { class: "hint c-bad" }, ui.detailsError.get(pr.number)));
        return wrap;
    }
    if (!details) {
        wrap.append(h("div", { class: "hint" }, "Loading checks…"));
        return wrap;
    }
    if (!details.checks.length) {
        wrap.append(h("div", { class: "hint" }, "No checks reported for the head commit."));
        return wrap;
    }
    const sorted = [...details.checks].sort((a, b) => checkRank(a.state) - checkRank(b.state) || a.name.localeCompare(b.name));
    wrap.append(
        h(
            "ul",
            { class: "checks-list" },
            sorted.map((c) =>
                h(
                    "li",
                    null,
                    checkIcon(c.state),
                    c.url ? h("a", { href: c.url, target: "_blank", rel: "noopener" }, c.workflow ? `${c.workflow} / ${c.name}` : c.name) : h("span", null, c.name),
                    c.required ? badge("required", "b-outline") : null,
                    h("span", { class: "muted small" }, (c.state ?? "").toLowerCase()),
                ),
            ),
        ),
    );
    return wrap;
}

const DIFF_AUTO_OPEN_LINES = 400;
const DIFF_AUTO_OPEN_FILES = 20;
const FILE_STATUS_MARK = { added: "A", removed: "D", renamed: "R", modified: "M" };

function addDel(additions, deletions) {
    return h("span", { class: "small diff-stat" }, h("span", { class: "c-add" }, `+${additions}`), " ", h("span", { class: "c-del" }, `−${deletions}`));
}

function renderFiles(pr, details) {
    const diff = ui.diffs.get(pr.number);
    const error = ui.diffError.get(pr.number);
    const loading = ui.diffLoading.has(pr.number);
    const wrap = h("div", { class: "diff-section" });
    wrap.append(
        h(
            "h3",
            null,
            "Files changed",
            badge(String(diff?.files.length ?? pr.changedFiles), "b-outline"),
            diff ? addDel(diff.additions, diff.deletions) : null,
            h("span", { class: "grow" }),
            diff ? h("button", { class: "btn btn-small", type: "button", onclick: () => toggleAllDiffs(wrap, true) }, "Expand all") : null,
            diff ? h("button", { class: "btn btn-small", type: "button", onclick: () => toggleAllDiffs(wrap, false) }, "Collapse all") : null,
            h("a", { class: "btn btn-small", href: `${pr.url}/files`, target: "_blank", rel: "noopener" }, "Open on GitHub ↗"),
        ),
    );

    if (!diff) {
        if (error) {
            wrap.append(h("div", { class: "callout warn" }, `Could not load the diff: ${error} `, h("button", { class: "btn btn-small", type: "button", onclick: () => loadDiff(pr.number, true) }, "Retry")));
        } else {
            wrap.append(h("div", { class: "muted small" }, loading ? "Loading diff…" : ""));
        }
        const files = details?.files ?? pr.files.map((path) => ({ path }));
        wrap.append(h("ul", { class: "files" }, files.map((f) => h("li", { class: "mono" }, f.path, f.additions !== undefined ? h("span", { class: "muted" }, ` +${f.additions} −${f.deletions}`) : null))));
        return wrap;
    }

    if (diff.headOid && pr.headOid && diff.headOid !== pr.headOid) {
        wrap.append(h("div", { class: "callout warn" }, loading ? "The PR has new commits. Reloading the diff…" : "The PR has new commits since this diff was loaded."));
    }
    if (diff.truncated) {
        wrap.append(h("div", { class: "callout warn" }, `This diff is very large, so only the first ${diff.totalLines.toLocaleString()} lines are shown. Open it on GitHub for the rest.`));
    }
    const autoOpen = diff.files.length <= DIFF_AUTO_OPEN_FILES;
    for (const f of diff.files) {
        wrap.append(renderDiffFile(pr, f, autoOpen && !f.lockfile && !f.binary && f.lineCount <= DIFF_AUTO_OPEN_LINES));
    }
    if (!diff.files.length) wrap.append(h("div", { class: "muted small" }, "This PR has no file changes."));
    return wrap;
}

function toggleAllDiffs(wrap, open) {
    for (const det of wrap.querySelectorAll("details.diff-file")) det.open = open;
}

function renderDiffFile(pr, f, defaultOpen) {
    const key = `${pr.number}:${f.path}`;
    const open = ui.diffOpen.has(key) ? ui.diffOpen.get(key) : defaultOpen;
    const body = h("div", { class: "diff-body" });
    const det = h(
        "details",
        { class: "diff-file", open: open || null },
        h(
            "summary",
            null,
            h("span", { class: `diff-status s-${f.status}`, title: f.status }, FILE_STATUS_MARK[f.status] ?? "M"),
            h("span", { class: "mono diff-path" }, f.oldPath && f.oldPath !== f.path ? `${f.oldPath} → ${f.path}` : f.path),
            f.lockfile ? badge("lockfile", "b-outline", "Generated lockfile; collapsed by default") : null,
            f.binary ? badge("binary", "b-outline") : null,
            f.truncated ? badge("truncated", "b-outline", "Only the first lines are shown; open on GitHub for the rest") : null,
            addDel(f.additions, f.deletions),
        ),
        body,
    );
    const fill = () => {
        if (!body.childElementCount) body.append(renderDiffTable(f));
    };
    if (open) fill();
    det.addEventListener("toggle", () => {
        ui.diffOpen.set(key, det.open);
        if (det.open) fill();
    });
    return det;
}

function renderDiffTable(f) {
    if (f.binary) return h("div", { class: "muted small diff-note" }, "Binary file not shown.");
    if (!f.hunks.length) return h("div", { class: "muted small diff-note" }, f.status === "renamed" ? "File renamed without content changes." : "No textual changes.");
    const rows = [];
    for (const hunk of f.hunks) {
        rows.push(h("tr", { class: "d-hunk" }, h("td", { class: "ln", colspan: 2 }), h("td", { class: "code" }, hunk.header)));
        let oldLine = hunk.oldStart;
        let newLine = hunk.newStart;
        for (const line of hunk.lines) {
            const marker = line[0];
            if (marker === "\\") {
                rows.push(h("tr", { class: "d-meta" }, h("td", { class: "ln", colspan: 2 }), h("td", { class: "code" }, line.slice(1).trim())));
                continue;
            }
            const cls = marker === "+" ? "d-add" : marker === "-" ? "d-del" : "d-ctx";
            rows.push(
                h(
                    "tr",
                    { class: cls },
                    h("td", { class: "ln" }, marker === "+" ? "" : String(oldLine)),
                    h("td", { class: "ln" }, marker === "-" ? "" : String(newLine)),
                    h("td", { class: "code" }, h("span", { class: "mk", "aria-hidden": "true" }, marker === " " ? " " : marker), line.slice(1)),
                ),
            );
            if (marker !== "+") oldLine++;
            if (marker !== "-") newLine++;
        }
    }
    const out = h("div", { class: "diff-scroll" }, h("table", { class: "diff" }, h("tbody", null, rows)));
    if (f.truncated) out.append(h("div", { class: "muted small diff-note" }, `Showing the first ${f.lineCount.toLocaleString()} lines of this file's diff.`));
    return out;
}

function renderReleaseNotes(bodyHTML) {
    const det = h("details", null, h("summary", null, "Release notes, changelog & commits (PR description)"));
    det.addEventListener("toggle", () => {
        if (!det.open || det.querySelector("iframe")) return;
        const cs = getComputedStyle(document.body);
        const doc = `<!doctype html><html><head><base target="_blank"><style>
            body{margin:8px 12px;font:13px/1.5 ${cs.fontFamily};color:${cs.color};background:${cs.backgroundColor}}
            a{color:${getComputedStyle(document.documentElement).getPropertyValue("--blue") || "#0969da"}}
            pre,code{font-size:12px;white-space:pre-wrap}
            img{max-width:100%}
            blockquote{margin:0 0 0 8px;padding-left:8px;border-left:2px solid ${cs.color}33}
        </style></head><body>${bodyHTML}</body></html>`;
        det.append(h("iframe", { class: "notes", sandbox: "allow-popups allow-popups-to-escape-sandbox", srcdoc: doc, title: "PR description" }));
    });
    return det;
}

// ---------------------------------------------------------------------------
// Rendering: alerts & overlaps tabs

function renderAlerts() {
    const el = $("alerts");
    el.replaceChildren();
    const s = ui.state;
    if (!s) return;
    if (s.alertsError) {
        el.append(h("div", { class: "callout warn" }, s.alertsError));
        return;
    }
    if (s.loading.alerts && !s.alerts.length) {
        el.append(h("div", { class: "empty" }, "Loading security alerts…"));
        return;
    }
    let alerts = [...s.alerts];
    if (ui.alertsUnaddressedOnly) alerts = alerts.filter((a) => !a.fixedBy.length);
    alerts.sort((a, b) => (SEVERITY_ORDER[a.severity] ?? 9) - (SEVERITY_ORDER[b.severity] ?? 9) || a.package.localeCompare(b.package));
    el.append(
        h("p", { class: "hint" }, `${s.counts.alerts} open Dependabot alert(s); ${s.counts.unaddressedAlerts} have no open PR that fixes them (often transitive dependencies — these need a manual bump or an override).`),
    );
    if (!alerts.length) {
        el.append(h("div", { class: "empty" }, "No alerts to show."));
        return;
    }
    el.append(
        h(
            "table",
            { class: "grid" },
            h("thead", null, h("tr", null, h("th", null, "Severity"), h("th", null, "Package"), h("th", null, "Advisory"), h("th", null, "Manifest"), h("th", null, "Fixed by"), h("th", null, "Related"))),
            h(
                "tbody",
                null,
                alerts.map((a) =>
                    h(
                        "tr",
                        null,
                        h("td", null, badge(a.severity, `b-${a.severity}`)),
                        h("td", null, h("code", null, a.package), h("div", { class: "muted small" }, `${a.ecosystem} · patched ${a.patched ?? "n/a"}`)),
                        h("td", null, h("a", { href: a.url, target: "_blank", rel: "noopener" }, a.ghsa ?? `#${a.number}`), h("div", { class: "small" }, a.summary)),
                        h("td", { class: "mono small" }, a.manifestPath, h("div", { class: "muted" }, a.relationship ?? "")),
                        h("td", null, a.fixedBy.length ? h("div", { class: "chips" }, a.fixedBy.map((n) => prChip(n))) : h("span", { class: "c-bad small" }, "No PR")),
                        h("td", null, h("div", { class: "chips" }, a.relatedPrs.map((n) => prChip(n)))),
                    ),
                ),
            ),
        ),
    );
}

function renderCombinedSection(records) {
    const canWrite = !!ui.state.repoInfo?.canWrite;
    const statusText = (c) => (c.status === "requested" ? "waiting for agent" : c.status === "failed" ? "agent gave up" : (c.prState ?? "unknown").toLowerCase());
    return h(
        "div",
        null,
        h("div", { class: "section-title" }, "Combined PRs", badge(String(records.length), "b-outline")),
        h("p", { class: "hint" }, "Groups the agent was asked to fold into a single PR. Entries clear themselves once the combined PR is closed or merged and none of its Dependabot PRs are still open."),
        h(
            "table",
            { class: "grid" },
            h("thead", null, h("tr", null, h("th", null, "Combined PR"), h("th", null, "Status"), h("th", null, "Replaces"), h("th", null, ""))),
            h(
                "tbody",
                null,
                records.map((c) => {
                    const members = c.status === "opened" ? c.included : c.numbers;
                    const open = members.filter((n) => prMap().has(n));
                    return h(
                        "tr",
                        null,
                        h(
                            "td",
                            null,
                            c.prNumber ? h("a", { href: c.prUrl, target: "_blank", rel: "noopener" }, `#${c.prNumber} ${c.prTitle ?? ""}`) : h("span", null, c.title ?? "(untitled)"),
                            h("div", { class: "muted small mono" }, c.branch ?? ""),
                            c.summary ? h("div", { class: "small" }, c.summary) : null,
                        ),
                        h("td", null, badge(statusText(c), c.prState === "MERGED" ? "b-safe" : c.status === "failed" || c.prState === "CLOSED" ? "b-blocked" : "b-combined"), h("div", { class: "muted small" }, ago(c.recordedAt ?? c.requestedAt))),
                        h(
                            "td",
                            null,
                            h("div", { class: "chips" }, members.map((n) => prChip(n))),
                            c.excluded.length ? h("div", { class: "muted small" }, `Left out: ${c.excluded.map((e) => `#${e.number}${e.reason ? ` (${e.reason})` : ""}`).join(", ")}`) : null,
                        ),
                        h(
                            "td",
                            null,
                            h(
                                "div",
                                { class: "btn-group" },
                                open.length ? h("button", { class: "btn btn-small", type: "button", onclick: () => { selectNumbers(open, true); switchTab("prs"); } }, "Select") : null,
                                c.status === "opened" && open.length ? h("button", { class: "btn btn-small btn-danger", type: "button", disabled: !canWrite, onclick: () => confirmClose(open, { supersededBy: c }) }, `Close ${open.length}…`) : null,
                                h("button", { class: "btn btn-small", type: "button", onclick: () => clearCombined(c.id) }, "Clear"),
                            ),
                        ),
                    );
                }),
            ),
        ),
    );
}

function renderOverlapsTab() {
    const el = $("overlaps");
    el.replaceChildren();
    const s = ui.state;
    if (!s) return;
    if (s.combined?.length) el.append(renderCombinedSection(s.combined));
    el.append(
        h("div", { class: "section-title" }, "Same dependency in multiple PRs", badge(String(s.dependencyClusters.length), "b-outline")),
        h("p", { class: "hint" }, "Review each dependency once and merge its PRs together. Divergent targets mean directories are being moved to different versions."),
    );
    if (s.dependencyClusters.length) {
        el.append(
            h(
                "table",
                { class: "grid" },
                h("thead", null, h("tr", null, h("th", null, "Dependency"), h("th", null, "Target"), h("th", null, "PRs"), h("th", null, ""))),
                h(
                    "tbody",
                    null,
                    s.dependencyClusters.map((c) =>
                        h(
                            "tr",
                            null,
                            h("td", null, h("code", null, c.name), h("div", { class: "muted small" }, c.ecosystem)),
                            h("td", null, c.targets.join(", "), c.divergent ? h("div", null, badge("divergent", "b-caution")) : null),
                            h("td", null, h("div", { class: "chips" }, c.prs.map((n) => prChip(n)))),
                            h(
                                "td",
                                null,
                                h(
                                    "div",
                                    { class: "btn-group" },
                                    h("button", { class: "btn btn-small", type: "button", onclick: () => { selectNumbers(c.prs, true); setFilters({ q: c.name }); switchTab("prs"); } }, "Select"),
                                    h("button", { class: "btn btn-small btn-accent", type: "button", onclick: () => confirmSend(c.prs) }, "✦ Review"),
                                    h("button", { class: "btn btn-small btn-accent", type: "button", title: "Combine into one PR", onclick: () => confirmCombine(c.prs) }, "⊕ Combine"),
                                ),
                            ),
                        ),
                    ),
                ),
            ),
        );
    }

    el.append(
        h("div", { class: "section-title" }, "Shared files — merge-conflict hotspots", badge(String(s.fileHotspots.length), "b-outline")),
        h("p", { class: "hint" }, "PRs that edit the same file (typically lockfiles or Directory.Packages.props) conflict once one of them merges. Dependabot rebases the rest automatically; enabling auto-merge lets them land one after another."),
    );
    const hotspots = s.fileHotspots.slice(0, ui.hotspotLimit);
    if (hotspots.length) {
        el.append(
            h(
                "table",
                { class: "grid" },
                h("thead", null, h("tr", null, h("th", null, "File"), h("th", null, "PRs"), h("th", null, ""))),
                h(
                    "tbody",
                    null,
                    hotspots.map((f) =>
                        h(
                            "tr",
                            null,
                            h("td", { class: "mono small" }, f.path),
                            h("td", null, h("div", { class: "chips" }, f.prs.map((n) => prChip(n)))),
                            h("td", null, h("button", { class: "btn btn-small", type: "button", onclick: () => selectNumbers(f.prs, true) }, "Select")),
                        ),
                    ),
                ),
            ),
        );
        if (s.fileHotspots.length > ui.hotspotLimit) {
            el.append(h("button", { class: "btn", type: "button", style: "margin-top:8px", onclick: () => { ui.hotspotLimit += 100; renderOverlapsTab(); } }, `Show more (${s.fileHotspots.length - ui.hotspotLimit} remaining)`));
        }
    }
}

// ---------------------------------------------------------------------------
// Bulk bar & jobs

function renderBulkbar() {
    const bar = $("bulkbar");
    const prs = prMap();
    for (const n of [...ui.checked]) if (!prs.has(n)) ui.checked.delete(n);
    const nums = [...ui.checked].sort((a, b) => a - b);
    if (!nums.length) {
        bar.hidden = true;
        return;
    }
    const canWrite = !!ui.state?.repoInfo?.canWrite;
    const cmdSelect = h("select", { title: "Dependabot command" }, h("option", { value: "rebase" }, "@dependabot rebase"), h("option", { value: "recreate" }, "@dependabot recreate"), h("option", { value: "ignore-major" }, "ignore this major version"), h("option", { value: "ignore-minor" }, "ignore this minor version"), h("option", { value: "ignore-patch" }, "ignore this patch version"), h("option", { value: "ignore-dependency" }, "ignore this dependency"));
    bar.hidden = false;
    bar.replaceChildren(
        h("span", { class: "label" }, `${nums.length} selected`),
        h("button", { class: "btn", type: "button", disabled: !canWrite, onclick: () => confirmApprove(nums) }, "✓ Approve"),
        h("button", { class: "btn btn-primary", type: "button", disabled: !canWrite, onclick: () => confirmMerge(nums, { approve: true }) }, "Approve & merge…"),
        h("button", { class: "btn btn-accent", type: "button", onclick: () => confirmSend(nums) }, "✦ Review with agent…"),
        h("button", { class: "btn btn-accent", type: "button", disabled: nums.length < 2, title: nums.length < 2 ? "Select at least two PRs" : "Ask an agent to combine the selected PRs into one PR", onclick: () => confirmCombine(nums) }, "⊕ Combine into one PR…"),
        cmdSelect,
        h("button", { class: "btn", type: "button", onclick: () => confirmDependabot(nums, cmdSelect.value) }, "Comment"),
        h("button", { class: "btn btn-danger", type: "button", disabled: !canWrite, onclick: () => confirmClose(nums) }, "Close…"),
        h("button", { class: "btn btn-small", type: "button", onclick: () => { ui.checked.clear(); renderAll(); } }, "Clear"),
    );
}

function renderJobs() {
    for (const job of ui.jobs.values()) {
        if (ui.dismissedJobs.has(job.id)) continue;
        const done = job.results.length;
        const failed = job.results.filter((r) => !r.ok);
        const finished = job.status !== "running";
        let el = document.querySelector(`[data-job="${job.id}"]`);
        const title = `${JOB_LABELS[job.type] ?? job.type} ${done}/${job.numbers.length}${finished ? (failed.length ? ` — ${failed.length} failed` : " — done") : "…"}`;
        const content = h(
            "div",
            null,
            h("div", { class: "title" }, h("span", null, title), h("button", { class: "close", type: "button", "aria-label": "Dismiss", onclick: () => { ui.dismissedJobs.add(job.id); el.remove(); } }, "✕")),
            h("div", { class: "progress" }, h("div", { style: `transform:scaleX(${job.numbers.length ? done / job.numbers.length : 1})` })),
            failed.length ? h("ul", null, failed.map((r) => h("li", { class: "fail" }, `#${r.number}: ${r.message}`))) : null,
            job.error ? h("div", { class: "fail" }, job.error) : null,
        );
        if (!el) {
            el = h("div", { class: "toast", "data-job": job.id });
            $("toasts").prepend(el);
        }
        el.className = `toast ${finished ? (failed.length || job.error ? "error" : "success") : ""}`;
        el.replaceChildren(content);
        if (finished && !failed.length && !job.error && !el.dataset.timer) {
            el.dataset.timer = "1";
            setTimeout(() => {
                ui.dismissedJobs.add(job.id);
                el.remove();
            }, 6000);
        }
    }
}

// ---------------------------------------------------------------------------
// Modals & actions

function openModal({ title, body, confirmLabel = "Confirm", confirmClass = "btn-primary", collect }) {
    return new Promise((resolve) => {
        const backdrop = $("modal");
        const close = (value) => {
            backdrop.hidden = true;
            backdrop.replaceChildren();
            document.removeEventListener("keydown", onKey);
            resolve(value);
        };
        const onKey = (e) => e.key === "Escape" && close(null);
        document.addEventListener("keydown", onKey);
        const confirmBtn = h("button", { class: `btn ${confirmClass}`, type: "button", onclick: () => close(collect ? collect() : true) }, confirmLabel);
        backdrop.replaceChildren(
            h(
                "div",
                { class: "modal", role: "dialog", "aria-modal": "true", "aria-label": title },
                h("h3", null, title),
                h("div", { class: "body" }, body),
                h("div", { class: "footer" }, h("button", { class: "btn", type: "button", onclick: () => close(null) }, "Cancel"), confirmBtn),
            ),
        );
        backdrop.onclick = (e) => e.target === backdrop && close(null);
        backdrop.hidden = false;
        confirmBtn.focus();
    });
}

function prListBlock(nums) {
    const prs = prMap();
    return h(
        "div",
        { class: "pr-list" },
        nums.slice(0, 100).map((n) => {
            const pr = prs.get(n);
            const d = pr ? depSummary(pr) : { name: "", versions: "" };
            return h("div", { class: "small" }, pr ? statusDot(pr.status) : null, ` #${n} `, h("b", null, d.name), ` ${d.versions}`, pr ? h("span", { class: "muted" }, ` · ${pr.directory}`) : null);
        }),
        nums.length > 100 ? h("div", { class: "muted small" }, `…and ${nums.length - 100} more`) : null,
    );
}

function selectionWarnings(nums) {
    const prs = prMap();
    const list = nums.map((n) => prs.get(n)).filter(Boolean);
    const warnings = [];
    const notReady = list.filter((p) => !["ready", "needs-review"].includes(p.status));
    if (notReady.length) warnings.push(`${notReady.length} PR(s) are not ready (${[...new Set(notReady.map((p) => STATUS[p.status]?.label))].join(", ")}).`);
    const majors = list.filter((p) => p.highestUpdateType === "major");
    if (majors.length) warnings.push(`${majors.length} PR(s) contain major version updates.`);
    const set = new Set(nums);
    const conflicting = list.filter((p) => p.overlaps.sharedFiles.some((o) => set.has(o.number)));
    if (conflicting.length) warnings.push(`${conflicting.length} selected PR(s) share files with each other — after the first merge the rest will conflict until Dependabot rebases them. Auto-merge handles this best.`);
    const blocked = list.filter((p) => p.agentReview?.verdict === "blocked");
    if (blocked.length) warnings.push(`${blocked.length} PR(s) were marked "blocked" by the agent review.`);
    return warnings.map((w) => h("div", { class: "callout warn" }, w));
}

async function runAction(type, numbers, options = {}) {
    try {
        await api("/api/action", { method: "POST", body: { type, numbers, options } });
    } catch (e) {
        toast(`Failed: ${e.message}`, { kind: "error" });
    }
}

async function confirmApprove(nums) {
    const bodyInput = h("textarea", { placeholder: "Optional review comment" });
    const ok = await openModal({
        title: `Approve ${nums.length} pull request${nums.length > 1 ? "s" : ""}`,
        body: [prListBlock(nums), ...selectionWarnings(nums), bodyInput],
        confirmLabel: "Approve",
        collect: () => ({ body: bodyInput.value.trim() }),
    });
    if (ok) runAction("approve", nums, ok);
}

async function confirmMerge(nums, { approve }) {
    const info = ui.state.repoInfo;
    const methods = info?.mergeMethods ?? ["merge"];
    const method = ui.mergeMethod && methods.includes(ui.mergeMethod) ? ui.mergeMethod : methods.includes("squash") ? "squash" : methods[0];
    const prs = prMap();
    const anyNotReady = nums.some((n) => !["ready", "needs-review"].includes(prs.get(n)?.status));
    const sharedWithin = nums.some((n) => prs.get(n)?.overlaps.sharedFiles.some((o) => nums.includes(o.number)));

    const radios = methods.map((m) => h("label", { class: "check" }, h("input", { type: "radio", name: "method", value: m, checked: m === method }), m));
    const approveBox = h("input", { type: "checkbox", checked: approve });
    const autoBox = h("input", { type: "checkbox", checked: !!info?.autoMergeAllowed && (anyNotReady || (nums.length > 1 && sharedWithin)), disabled: !info?.autoMergeAllowed });
    const deleteBox = h("input", { type: "checkbox", checked: !info?.deleteBranchOnMerge });

    const ok = await openModal({
        title: `${approve ? "Approve & merge" : "Merge"} ${nums.length} pull request${nums.length > 1 ? "s" : ""}`,
        body: [
            prListBlock(nums),
            ...selectionWarnings(nums),
            h("fieldset", null, h("legend", null, "Merge method"), radios),
            h("label", { class: "check" }, approveBox, "Approve (sign off) before merging"),
            h("label", { class: "check", title: info?.autoMergeAllowed ? "" : "Auto-merge is disabled in this repository's settings" }, autoBox, "Use auto-merge — merge once required checks & reviews pass"),
            info?.deleteBranchOnMerge ? h("div", { class: "muted small" }, "Branches are deleted automatically on merge (repository setting).") : h("label", { class: "check" }, deleteBox, "Delete branch after merge"),
            h("div", { class: "muted small" }, "Merges use --match-head-commit, so a PR that received new commits since this view was loaded will not be merged."),
        ],
        confirmLabel: `${approve ? "Approve & merge" : "Merge"} ${nums.length}`,
        collect: () => {
            const chosen = radios.map((r) => r.querySelector("input")).find((i) => i.checked)?.value ?? method;
            ui.mergeMethod = chosen;
            return { method: chosen, approve: approveBox.checked, auto: autoBox.checked, deleteBranch: !info?.deleteBranchOnMerge && deleteBox.checked };
        },
    });
    if (ok) runAction(ok.approve ? "approve-merge" : "merge", nums, ok);
}

function targetPicker() {
    const active = ui.agents.filter((a) => ACTIVE_AGENT.has(a.status));
    const select = h(
        "select",
        { "aria-label": "Run in" },
        h("option", { value: "new" }, "New background agent — runs in parallel"),
        active.map((a) => h("option", { value: a.agentId }, `Existing agent: ${a.name} (${AGENT_STATUS[a.status]?.label ?? a.status})`)),
        h("option", { value: "main" }, "This session — queued behind current work"),
    );
    select.value = ui.lastTarget === "main" ? "main" : "new";
    return {
        el: h("label", { class: "field" }, h("span", null, "Run in"), select),
        value: () => {
            const v = select.value;
            if (v === "new" || v === "main") ui.lastTarget = v;
            return v;
        },
    };
}

function dispatchToast(result, what) {
    if (result?.fallback) toast(`${what}: sent to this session instead`, { kind: "warn", body: `Couldn't start a background agent (${result.fallback}).` });
    else if (result?.target === "agent") toast(result.created ? `Started agent “${result.name}”` : `Sent to agent “${result.name}”`, { kind: "success", body: what });
    else toast(`Sent to this session`, { kind: "success", body: what });
}

async function confirmSend(nums) {
    const note = h("textarea", { placeholder: "Optional: extra instructions for the agent (e.g. check for API changes in src/…)" });
    const picker = targetPicker();
    const ok = await openModal({
        title: `Send ${nums.length} PR${nums.length > 1 ? "s" : ""} to an agent for review`,
        body: [
            h("div", { class: "hint" }, "The agent gets a review prompt with the PR details, checks, alerts and overlaps and records its verdict back into this canvas. It will not approve or merge. A background agent runs alongside this session, so you can start several at once and follow them on the Agents tab."),
            prListBlock(nums),
            picker.el,
            note,
        ],
        confirmLabel: "Send",
        confirmClass: "btn-primary",
        collect: () => ({ note: note.value.trim(), target: picker.value() }),
    });
    if (!ok) return;
    try {
        const result = await api("/api/send", { method: "POST", body: { numbers: nums, note: ok.note, target: ok.target } });
        dispatchToast(result, `Review of ${nums.length} PR(s)`);
    } catch (e) {
        toast(`Could not send: ${e.message}`, { kind: "error" });
    }
}

function slug(text, max = 50) {
    return text.toLowerCase().replace(/^@/, "").replace(/[^a-z0-9.]+/g, "-").replace(/^[-.]+|[-.]+$/g, "").slice(0, max).replace(/[-.]+$/, "");
}

function combineSuggestion(nums) {
    const list = nums.map((n) => prMap().get(n)).filter(Boolean);
    const single = list.every((p) => p.updates.length === 1);
    const names = new Set(list.flatMap((p) => p.updates.map((u) => u.name.toLowerCase())));
    const where = (() => {
        const dirs = new Set(list.map((p) => p.directory));
        return dirs.size > 1 ? ` in ${dirs.size} directories` : dirs.size === 1 && [...dirs][0] !== "/" ? ` in ${[...dirs][0]}` : "";
    })();
    if (single && names.size === 1) {
        const u = list[0].updates[0];
        const tos = new Set(list.map((p) => p.updates[0].to));
        const froms = new Set(list.map((p) => p.updates[0].from));
        const to = tos.size === 1 ? u.to : null;
        const from = froms.size === 1 ? u.from : null;
        return {
            title: `Bump ${u.name}${from && to ? ` from ${from}` : ""}${to ? ` to ${to}` : ""}${where}`,
            branch: `deps/combine-${slug(u.name)}${to ? `-${slug(to, 30)}` : ""}`,
        };
    }
    const groups = new Set(list.map((p) => p.groupName));
    if (groups.size === 1 && list[0].groupName) {
        return { title: `Bump the ${list[0].groupName} group${where}`, branch: `deps/combine-${slug(list[0].groupName)}` };
    }
    const date = new Date().toISOString().slice(0, 10).replace(/-/g, "");
    return { title: `Combined Dependabot updates (${list.length} PRs)`, branch: `deps/combined-${date}` };
}

function combineWarnings(nums) {
    const prs = prMap();
    const list = nums.map((n) => prs.get(n)).filter(Boolean);
    const warnings = [];
    const ecos = new Set(list.map((p) => p.ecosystem));
    if (ecos.size > 1) warnings.push(`Mixes ${ecos.size} ecosystems (${[...ecos].join(", ")}); a combined PR is easier to review within one ecosystem.`);
    const byDep = new Map();
    for (const p of list) for (const u of p.updates) {
        const k = u.name.toLowerCase();
        if (!byDep.has(k)) byDep.set(k, new Set());
        byDep.get(k).add(u.to);
    }
    const divergent = [...byDep.entries()].filter(([, tos]) => tos.size > 1);
    if (divergent.length) warnings.push(`Different target versions for ${divergent.slice(0, 3).map(([k, tos]) => `${k} (${[...tos].join(", ")})`).join("; ")}${divergent.length > 3 ? "…" : ""}.`);
    const conflicting = list.filter((p) => p.mergeable === "CONFLICTING");
    if (conflicting.length) warnings.push(`${conflicting.length} PR(s) currently conflict with ${ui.state.repoInfo?.defaultBranch ?? "the base branch"}; the agent will regenerate their lockfiles.`);
    const failing = list.filter((p) => p.status === "failing");
    if (failing.length) warnings.push(`${failing.length} PR(s) have failing checks — the combined PR will likely fail the same way unless you ask the agent to fix breakages.`);
    const majors = list.filter((p) => p.highestUpdateType === "major");
    if (majors.length) warnings.push(`${majors.length} PR(s) contain major version updates.`);
    const already = list.filter((p) => p.combine);
    if (already.length) warnings.push(`${already.length} PR(s) are already part of another combine request (${[...new Set(already.map((p) => (p.combine.prNumber ? `#${p.combine.prNumber}` : "pending")))].join(", ")}).`);
    return warnings.map((w) => h("div", { class: "callout warn" }, w));
}

async function confirmCombine(nums) {
    nums = [...new Set(nums)].filter((n) => prMap().has(n)).sort((a, b) => a - b);
    if (nums.length < 2) return toast("Pick at least two open PRs to combine.", { kind: "error" });
    if (nums.length > 150) return toast("Combine at most 150 PRs at a time — narrow the selection.", { kind: "error" });
    const suggestion = combineSuggestion(nums);
    const titleInput = h("input", { type: "text", value: suggestion.title, maxlength: 200, "aria-label": "Pull request title" });
    const branchInput = h("input", { type: "text", value: suggestion.branch, maxlength: 100, class: "mono", "aria-label": "Branch name" });
    const draftBox = h("input", { type: "checkbox" });
    const fixBox = h("input", { type: "checkbox" });
    const note = h("textarea", { placeholder: "Optional: extra instructions for the agent (e.g. run the TypeScript AppHost tests)" });
    const base = ui.state.repoInfo?.defaultBranch ?? "main";
    const picker = targetPicker();
    const ok = await openModal({
        title: `Combine ${nums.length} PRs into one`,
        body: [
            h(
                "div",
                { class: "hint" },
                `An agent cherry-picks these PRs onto a new branch from ${base} (in a separate git worktree), regenerates lockfiles where they conflict, pushes it and opens a single PR as you. It won't approve, merge, close or comment on the originals. After the combined PR merges, Dependabot only closes the superseded PRs on its next scheduled run, so the canvas offers to close them for you.`,
            ),
            prListBlock(nums),
            ...combineWarnings(nums),
            h("label", { class: "field" }, h("span", null, "PR title"), titleInput),
            h("label", { class: "field" }, h("span", null, "Branch"), branchInput),
            h("div", { class: "btn-group" }, h("label", { class: "check" }, draftBox, "Open as draft"), h("label", { class: "check" }, fixBox, "Also fix code/build breaks caused by the updates")),
            picker.el,
            note,
        ],
        confirmLabel: "Start combine",
        collect: () => ({ title: titleInput.value.trim(), branch: branchInput.value.trim(), draft: draftBox.checked, fixBreakages: fixBox.checked, note: note.value.trim(), target: picker.value() }),
    });
    if (!ok) return;
    try {
        const result = await api("/api/combine", { method: "POST", body: { numbers: nums, ...ok } });
        dispatchToast(result, `Combine ${nums.length} PRs → ${ok.branch}`);
    } catch (e) {
        toast(`Could not start combine: ${e.message}`, { kind: "error" });
    }
}

function combineRecord(id) {
    return (ui.state?.combined ?? []).find((c) => c.id === id) ?? null;
}

function renderCombineCallout(pr, canWrite) {
    const rec = combineRecord(pr.combine.id);
    if (!rec) return null;
    const open = (rec.status === "opened" ? rec.included : rec.numbers).filter((n) => prMap().has(n));
    if (rec.status === "requested") {
        return h(
            "div",
            { class: "callout combine" },
            h("div", null, badge("combine requested", "b-combined"), " ", h("span", { class: "muted" }, `${ago(rec.requestedAt)} · ${rec.numbers.length} PRs → `), h("code", null, rec.branch ?? "?"), " ", h("button", { class: "btn btn-small", type: "button", title: "Forget this request (e.g. if the agent gave up)", onclick: () => clearCombined(rec.id) }, "Clear")),
            h("p", { class: "muted" }, "Waiting for the agent to open the combined PR."),
        );
    }
    const state = (rec.prState ?? "unknown").toLowerCase();
    return h(
        "div",
        { class: "callout combine" },
        h(
            "div",
            null,
            badge(`superseded by #${rec.prNumber}`, "b-combined"),
            " ",
            h("a", { href: rec.prUrl, target: "_blank", rel: "noopener" }, rec.prTitle ?? `#${rec.prNumber}`),
            h("span", { class: "muted" }, ` · ${state} · includes ${rec.included.length} PR(s)${rec.excluded.length ? `, left out ${rec.excluded.length}` : ""}`),
        ),
        rec.summary ? h("p", null, rec.summary) : null,
        rec.excluded.some((e) => e.number === pr.number) ? h("p", { class: "c-bad" }, `This PR was left out: ${rec.excluded.find((e) => e.number === pr.number).reason}`) : null,
        h(
            "div",
            { class: "btn-group" },
            h("button", { class: "btn btn-small", type: "button", disabled: !canWrite || !open.length, title: "Close the Dependabot PRs included in the combined PR", onclick: () => confirmClose(open, { supersededBy: rec }) }, `Close ${open.length} superseded…`),
            h("button", { class: "btn btn-small", type: "button", onclick: () => clearCombined(rec.id) }, "Clear"),
        ),
    );
}

async function clearCombined(id) {
    try {
        await api("/api/combined/clear", { method: "POST", body: { id } });
    } catch (e) {
        toast(`Could not clear: ${e.message}`, { kind: "error" });
    }
}

async function confirmDependabot(nums, command, dependency) {
    const cmd = ui.state.dependabotCommands[command];
    const destructive = command.startsWith("ignore") || command === "recreate";
    const ok = await openModal({
        title: `${cmd?.label ?? command} on ${nums.length} PR${nums.length > 1 ? "s" : ""}`,
        body: [
            h("div", { class: "hint" }, "Posts a Dependabot command comment on each PR."),
            destructive ? h("div", { class: "callout warn" }, command === "recreate" ? "Recreate discards any manual commits on the PR branch." : "Ignore commands close the PR and stop Dependabot from proposing matching updates until unignored.") : null,
            prListBlock(nums),
        ],
        confirmLabel: "Post comment",
        confirmClass: destructive ? "btn-danger solid" : "btn-primary",
    });
    if (ok) runAction("dependabot", nums, { command, dependency });
}

async function confirmClose(nums, { supersededBy } = {}) {
    const comment = h("textarea", { placeholder: "Optional closing comment" });
    if (supersededBy) comment.value = `Superseded by #${supersededBy.prNumber}.`;
    const notMerged = supersededBy && supersededBy.prState !== "MERGED";
    const ok = await openModal({
        title: `Close ${nums.length} pull request${nums.length > 1 ? "s" : ""}`,
        body: [
            supersededBy
                ? h("div", { class: "hint" }, `These PRs are included in #${supersededBy.prNumber}. "Supersedes" isn't a closing keyword, so merging it doesn't close them; Dependabot only closes them on its next scheduled run for that ecosystem (which can be up to a week away). Close them here to tidy up now.`)
                : h("div", { class: "hint" }, "Dependabot will re-open a new PR for the next version. To stop that, use an \"ignore\" Dependabot command instead."),
            notMerged ? h("div", { class: "callout warn" }, `#${supersededBy.prNumber} is ${String(supersededBy.prState ?? "not merged").toLowerCase()}, not merged. If it doesn't merge, Dependabot won't re-propose these exact versions after you close them.`) : null,
            prListBlock(nums),
            comment,
        ],
        confirmLabel: "Close",
        confirmClass: "btn-danger solid",
        collect: () => ({ comment: comment.value.trim() }),
    });
    if (ok) runAction("close", nums, ok);
}

// ---------------------------------------------------------------------------
// Navigation & state sync

async function loadDetails(number, force = false) {
    ui.detailsLoading.add(number);
    ui.detailsError.delete(number);
    try {
        const d = await api(`/api/pr/${number}${force ? "?force=1" : ""}`);
        ui.details.set(number, d);
    } catch (e) {
        ui.detailsError.set(number, `Could not load details: ${e.message}`);
    } finally {
        ui.detailsLoading.delete(number);
        if (ui.selected === number) renderDetail();
    }
}

const MAX_CLIENT_DIFFS = 15;

async function loadDiff(number, force = false) {
    if (ui.diffLoading.has(number)) return;
    ui.diffLoading.add(number);
    ui.diffError.delete(number);
    try {
        const diff = await api(`/api/pr/${number}/diff${force ? "?force=1" : ""}`);
        ui.diffs.delete(number);
        ui.diffs.set(number, diff);
        while (ui.diffs.size > MAX_CLIENT_DIFFS) ui.diffs.delete(ui.diffs.keys().next().value);
    } catch (e) {
        ui.diffError.set(number, e.message);
    } finally {
        ui.diffLoading.delete(number);
        if (ui.selected === number) renderDetail();
    }
}

function focusPr(number, { scroll = true } = {}) {
    ui.selected = number;
    document.body.classList.add("show-detail");
    if (ui.tab !== "prs") switchTab("prs");
    const pr = prMap().get(number);
    if (pr && !matches(pr)) setFilters({ q: "", status: "", ecosystem: "", updateType: "", securityOnly: false });
    renderList();
    renderDetail();
    $("detail").scrollTop = 0;
    if (scroll) document.querySelector(`.row[data-number="${number}"]`)?.scrollIntoView({ block: "nearest" });
    postUiState();
}

function selectNumbers(numbers, replace) {
    if (replace) ui.checked.clear();
    for (const n of numbers) ui.checked.add(n);
    renderAll();
}

function setFilters(partial) {
    Object.assign(ui.filters, partial);
    $("f-q").value = ui.filters.q;
    $("f-status").value = ui.filters.status;
    $("f-ecosystem").value = ui.filters.ecosystem;
    $("f-update").value = ui.filters.updateType;
    $("f-security").checked = ui.filters.securityOnly;
    renderAll();
}

function switchTab(tab) {
    ui.tab = tab;
    for (const b of document.querySelectorAll(".tab")) b.classList.toggle("active", b.dataset.tab === tab);
    $("view-prs").hidden = tab !== "prs";
    $("view-alerts").hidden = tab !== "alerts";
    $("view-overlaps").hidden = tab !== "overlaps";
    $("view-agents").hidden = tab !== "agents";
    renderAll();
}

// ---------------------------------------------------------------------------
// Rendering: agents

const agentOpen = new Set();

async function agentCall(path, body, okMessage) {
    try {
        await api(path, { method: "POST", body });
        if (okMessage) toast(okMessage, { kind: "success" });
    } catch (e) {
        toast(e.message, { kind: "error" });
    }
}

async function messageAgent(a) {
    const input = h("textarea", { placeholder: "e.g. Also run the TypeScript AppHost tests before opening the PR", rows: 5 });
    const ok = await openModal({
        title: `Message “${a.name}”`,
        body: [h("div", { class: "hint" }, a.status === "running" ? "The agent is busy; your message is delivered when it finishes its current step." : "The agent picks this up as its next turn. Note: the runtime only reports a follow-up as finished once this Copilot session is between turns, so the status here may lag while the main session is busy."), input],
        confirmLabel: "Send",
        collect: () => input.value.trim(),
    });
    if (ok) agentCall("/api/agents/message", { agentId: a.agentId, message: ok }, `Sent to “${a.name}”`);
}

async function cancelAgent(a) {
    const ok = await openModal({
        title: `Cancel “${a.name}”?`,
        body: h("div", { class: "hint" }, "The agent stops immediately. Anything it already pushed or opened on GitHub stays as it is."),
        confirmLabel: "Cancel agent",
        confirmClass: "btn-danger solid",
    });
    if (ok) agentCall("/api/agents/cancel", { agentId: a.agentId }, `Cancelled “${a.name}”`);
}

function renderAgentsTab() {
    const el = $("agents");
    el.replaceChildren(
        h(
            "div",
            { class: "agents-head" },
            h("p", { class: "muted" }, "Background agents started from this canvas. Each runs alongside this Copilot session, so reviews and combines can proceed in parallel. Results are recorded back into the canvas automatically."),
            h("button", { class: "btn btn-small", type: "button", onclick: () => agentCall("/api/agents/refresh", {}) }, "Refresh"),
        ),
    );
    if (!ui.agents.length) {
        el.append(h("div", { class: "empty" }, "No agents yet. Use “Send to agent…” or “Combine…” on a PR, group or selection."));
        return;
    }
    const order = { running: 0, idle: 1, failed: 2, completed: 3, cancelled: 4, gone: 5 };
    const sorted = [...ui.agents].sort((x, y) => (order[x.status] ?? 9) - (order[y.status] ?? 9) || String(y.lastPromptAt).localeCompare(String(x.lastPromptAt)));
    for (const a of sorted) el.append(renderAgentCard(a));
}

function renderAgentCard(a) {
    const st = AGENT_STATUS[a.status] ?? { label: a.status, cls: "b-outline" };
    const prs = prMap();
    const active = ACTIVE_AGENT.has(a.status);
    const open = agentOpen.has(a.agentId);
    const chips = a.numbers.slice(0, 40).map((n) => (prs.has(n) ? prChip(n) : h("span", { class: "chip muted", title: "Not an open Dependabot PR any more" }, `#${n}`)));
    return h(
        "div",
        { class: `agent-card s-${a.status}` },
        h(
            "div",
            { class: "agent-title" },
            badge(st.label, st.cls),
            h("b", null, a.name),
            h("span", { class: "muted small" }, `${a.kinds.join(", ")} · started ${ago(a.startedAt)}${a.lastPromptAt !== a.startedAt ? ` · last prompt ${ago(a.lastPromptAt)}` : ""}${a.completedAt ? ` · finished ${ago(a.completedAt)}` : ""}`),
        ),
        chips.length ? h("div", { class: "chips" }, chips, a.numbers.length > 40 ? h("span", { class: "muted small" }, `+${a.numbers.length - 40} more`) : null) : null,
        a.error ? h("div", { class: "callout warn" }, a.error) : a.latestIntent && a.status === "running" ? h("div", { class: "muted small" }, a.latestIntent) : null,
        a.latestResponse
            ? h(
                  "details",
                  { class: "agent-response", open, ontoggle: (e) => (e.target.open ? agentOpen.add(a.agentId) : agentOpen.delete(a.agentId)) },
                  h("summary", null, "Latest response"),
                  h("pre", null, a.latestResponse),
              )
            : null,
        h(
            "div",
            { class: "btn-group" },
            h("button", { class: "btn btn-small", type: "button", disabled: !active, title: active ? "" : "This agent has finished; start a new one instead", onclick: () => messageAgent(a) }, "Message…"),
            active ? h("button", { class: "btn btn-small", type: "button", onclick: () => cancelAgent(a) }, "Cancel") : null,
            a.status !== "running" ? h("button", { class: "btn btn-small", type: "button", title: "Remove from this list", onclick: () => agentCall("/api/agents/dismiss", { agentId: a.agentId }) }, "Dismiss") : null,
        ),
    );
}

let uiStateTimer = null;
function postUiState() {
    clearTimeout(uiStateTimer);
    uiStateTimer = setTimeout(() => {
        api("/api/ui-state", {
            method: "POST",
            body: { tab: ui.tab, selected: ui.selected, checked: [...ui.checked], filters: ui.filters, groupBy: ui.groupBy, visible: visiblePrs().length },
        }).catch(() => {});
    }, 400);
}

function renderAll() {
    renderHeader();
    if (ui.tab === "prs") {
        renderList();
        renderDetail();
    } else if (ui.tab === "alerts") renderAlerts();
    else if (ui.tab === "agents") renderAgentsTab();
    else renderOverlapsTab();
    renderBulkbar();
    postUiState();
}

function handleCommand(cmd) {
    if (cmd.type === "focus") focusPr(cmd.number);
    else if (cmd.type === "select") selectNumbers(cmd.numbers, cmd.replace);
    else if (cmd.type === "filters") {
        const { type, groupBy, tab, ...filters } = cmd;
        if (groupBy) {
            ui.groupBy = groupBy;
            $("f-group").value = groupBy;
        }
        if (Object.keys(filters).length) setFilters(filters);
        if (tab) switchTab(tab);
        else renderAll();
    }
}

// ---------------------------------------------------------------------------
// Wiring

function wireControls() {
    for (const b of document.querySelectorAll(".tab")) b.addEventListener("click", () => switchTab(b.dataset.tab));
    $("refresh-btn").addEventListener("click", () => {
        ui.details.clear();
        api("/api/refresh", { method: "POST", body: {} }).catch((e) => toast(e.message, { kind: "error" }));
    });
    let qTimer;
    $("f-q").addEventListener("input", (e) => {
        clearTimeout(qTimer);
        qTimer = setTimeout(() => setFilters({ q: e.target.value }), 150);
    });
    $("f-status").addEventListener("change", (e) => setFilters({ status: e.target.value }));
    $("f-ecosystem").addEventListener("change", (e) => setFilters({ ecosystem: e.target.value }));
    $("f-update").addEventListener("change", (e) => setFilters({ updateType: e.target.value }));
    $("f-security").addEventListener("change", (e) => setFilters({ securityOnly: e.target.checked }));
    $("f-group").addEventListener("change", (e) => {
        ui.groupBy = e.target.value;
        ui.collapsed.clear();
        renderAll();
    });
    $("select-ready").addEventListener("click", () => {
        const ready = visiblePrs().filter((p) => p.status === "ready" || p.status === "needs-review").map((p) => p.number);
        selectNumbers(ready, true);
        toast(ready.length ? `Selected ${ready.length} ready PR(s)` : "No ready PRs in the current view");
    });
    $("a-unaddressed").addEventListener("change", (e) => {
        ui.alertsUnaddressedOnly = e.target.checked;
        renderAlerts();
    });

    document.addEventListener("keydown", (e) => {
        if (ui.tab !== "prs" || !$("modal").hidden) return;
        if (["INPUT", "SELECT", "TEXTAREA"].includes(document.activeElement?.tagName)) return;
        if (e.key !== "j" && e.key !== "k" && e.key !== "ArrowDown" && e.key !== "ArrowUp") return;
        const rows = [...document.querySelectorAll(".row")].map((r) => Number(r.dataset.number));
        if (!rows.length) return;
        const idx = rows.indexOf(ui.selected);
        const next = e.key === "j" || e.key === "ArrowDown" ? Math.min(rows.length - 1, idx + 1) : Math.max(0, idx - 1);
        e.preventDefault();
        focusPr(rows[next < 0 ? 0 : next]);
    });

    setInterval(renderHeader, 30_000);
}

function connect() {
    const es = new EventSource(`/events?token=${encodeURIComponent(token)}`);
    es.addEventListener("state", (e) => {
        ui.state = JSON.parse(e.data);
        // Drop cached details whose head commit changed.
        for (const pr of ui.state.prs) {
            const d = ui.details.get(pr.number);
            if (d && d.headOid && pr.headOid && d.headOid !== pr.headOid) ui.details.delete(pr.number);
        }
        renderAll();
    });
    es.addEventListener("job", (e) => {
        const job = JSON.parse(e.data);
        ui.jobs.set(job.id, job);
        renderJobs();
        if (job.status !== "running") for (const n of job.numbers) ui.details.delete(n);
    });
    es.addEventListener("command", (e) => handleCommand(JSON.parse(e.data)));
    es.addEventListener("agents", (e) => {
        ui.agents = JSON.parse(e.data) ?? [];
        if (ui.state) renderAll();
    });
    es.onerror = () => {
        $("refresh-status").textContent = "Reconnecting…";
    };
}

wireControls();
switchTab("prs");
connect();
