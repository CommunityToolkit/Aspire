// Pure data helpers: parse Dependabot PR metadata, match security alerts, and
// compute overlaps between PRs. No I/O in this module.

const ECOSYSTEM_FROM_BRANCH = {
    npm_and_yarn: "npm",
    github_actions: "actions",
    go_modules: "go",
    cargo: "rust",
    bundler: "rubygems",
    gradle: "maven",
    docker_compose: "docker",
};

const ECOSYSTEM_FROM_ALERT = {
    actions: "actions",
    golang: "go",
    go: "go",
    rust: "rust",
    cargo: "rust",
    rubygems: "rubygems",
};

export function normalizeBranchEcosystem(segment) {
    if (!segment) return "unknown";
    return ECOSYSTEM_FROM_BRANCH[segment] ?? segment;
}

export function normalizeAlertEcosystem(eco) {
    if (!eco) return "unknown";
    const lower = eco.toLowerCase();
    return ECOSYSTEM_FROM_ALERT[lower] ?? lower;
}

// ---------------------------------------------------------------------------
// Versions

function parseVersion(v) {
    if (!v) return null;
    const cleaned = String(v).trim().replace(/^[v=]/i, "");
    const m = cleaned.match(/^(\d+(?:\.\d+)*)(?:-([0-9A-Za-z.-]+))?/);
    if (!m) return null;
    return { parts: m[1].split(".").map(Number), pre: m[2] ?? null };
}

export function compareVersions(a, b) {
    const pa = parseVersion(a);
    const pb = parseVersion(b);
    if (!pa || !pb) return 0;
    const len = Math.max(pa.parts.length, pb.parts.length);
    for (let i = 0; i < len; i++) {
        const x = pa.parts[i] ?? 0;
        const y = pb.parts[i] ?? 0;
        if (x !== y) return x < y ? -1 : 1;
    }
    if (pa.pre && !pb.pre) return -1;
    if (!pa.pre && pb.pre) return 1;
    if (pa.pre && pb.pre) return pa.pre.localeCompare(pb.pre, undefined, { numeric: true });
    return 0;
}

export function classifyUpdate(from, to) {
    const pa = parseVersion(from);
    const pb = parseVersion(to);
    if (!pa || !pb) return "other";
    if ((pa.parts[0] ?? 0) !== (pb.parts[0] ?? 0)) return "major";
    if ((pa.parts[1] ?? 0) !== (pb.parts[1] ?? 0)) return "minor";
    return "patch";
}

const UPDATE_RANK = { major: 3, minor: 2, patch: 1, other: 0 };

// ---------------------------------------------------------------------------
// Dependabot metadata parsing

function unquote(s) {
    return String(s ?? "").trim().replace(/^['"]|['"]$/g, "");
}

// Dependabot commits end with a YAML block:
//   updated-dependencies:
//   - dependency-name: tsx
//     dependency-version: 4.23.15
//     dependency-type: direct:development
//     update-type: version-update:semver-patch
//   ...
export function parseUpdatedDependencies(message) {
    if (!message) return [];
    const idx = message.indexOf("updated-dependencies:");
    if (idx < 0) return [];
    const lines = message.slice(idx).split(/\r?\n/).slice(1);
    const deps = [];
    let current = null;
    for (const line of lines) {
        if (/^\.\.\.\s*$/.test(line) || /^Signed-off-by:/i.test(line)) break;
        let m = line.match(/^-\s+dependency-name:\s*(.+)$/);
        if (m) {
            current = { name: unquote(m[1]) };
            deps.push(current);
            continue;
        }
        m = line.match(/^\s+([a-z-]+):\s*(.*)$/);
        if (m && current) current[m[1]] = unquote(m[2]);
    }
    return deps;
}

// "Bumps [tsx](...) from 4.23.1 to 4.23.15." / "Updates `Foo` from 1.0 to 2.0"
export function parseFromToVersions(text) {
    const result = new Map();
    if (!text) return result;
    const re = /(?:Bumps|Updates|bump)\s+(?:\[([^\]]+)\]\([^)]*\)|`([^`]+)`|(\S+))\s+from\s+`?([^\s`]+?)`?\s+to\s+`?([^\s`]+?)`?(?=[.,]?(?:\s|$))/gi;
    let m;
    while ((m = re.exec(text)) !== null) {
        const name = (m[1] ?? m[2] ?? m[3] ?? "").replace(/`/g, "").trim();
        if (!name) continue;
        const key = name.toLowerCase();
        if (!result.has(key)) result.set(key, { from: m[4].replace(/\.$/, ""), to: m[5].replace(/\.$/, "") });
    }
    return result;
}

function commonDirectory(paths) {
    if (!paths.length) return "/";
    const dirs = paths.map((p) => p.split("/").slice(0, -1));
    const first = dirs[0];
    let len = first.length;
    for (const d of dirs) {
        let i = 0;
        while (i < len && d[i] === first[i]) i++;
        len = i;
    }
    return "/" + first.slice(0, len).join("/");
}

function dirname(path) {
    const idx = path.lastIndexOf("/");
    return idx < 0 ? "" : path.slice(0, idx);
}

export function normalizePr(node) {
    const message = node.first?.nodes?.[0]?.commit?.message ?? "";
    const headCommit = node.head?.nodes?.[0]?.commit;
    const branch = node.headRefName ?? "";
    const branchParts = branch.split("/");
    const ecosystem = normalizeBranchEcosystem(branchParts[0] === "dependabot" ? branchParts[1] : null);
    const files = (node.files?.nodes ?? []).map((f) => f.path);

    const title = node.title ?? "";
    const titleSingle = title.match(/bump\s+(\S+)\s+from\s+(\S+)\s+to\s+(\S+?)(?:\s+in\s+(\S+))?\s*$/i);
    const titleGroup = title.match(/bump\s+the\s+(\S+)\s+group(?:\s+across\s+\d+\s+directories)?(?:\s+in\s+(\S+))?\s+with\s+(\d+)\s+updates?/i);

    const yamlDeps = parseUpdatedDependencies(message);
    const versions = parseFromToVersions(`${title}\n${message}`);

    let updates = yamlDeps.map((d) => {
        const known = versions.get(d.name.toLowerCase());
        const to = d["dependency-version"] || known?.to || null;
        const from = known?.from ?? null;
        const typeMatch = (d["update-type"] ?? "").match(/semver-(major|minor|patch)/);
        return {
            name: d.name,
            from,
            to,
            type: typeMatch ? typeMatch[1] : classifyUpdate(from, to),
            dependencyType: d["dependency-type"] ?? null,
            group: d["dependency-group"] ?? null,
        };
    });
    if (!updates.length && titleSingle) {
        updates = [{
            name: titleSingle[1],
            from: titleSingle[2],
            to: titleSingle[3],
            type: classifyUpdate(titleSingle[2], titleSingle[3]),
            dependencyType: null,
            group: null,
        }];
    }

    let directory = titleSingle?.[4] ?? titleGroup?.[2] ?? null;
    if (!directory) directory = commonDirectory(files);
    if (!directory.startsWith("/")) directory = "/" + directory;

    const seenUpdates = new Set();
    updates = updates.filter((u) => {
        const key = `${u.name.toLowerCase()}|${u.from}|${u.to}`;
        if (seenUpdates.has(key)) return false;
        seenUpdates.add(key);
        return true;
    });

    const highestUpdateType = updates.reduce(
        (best, u) => (UPDATE_RANK[u.type] > UPDATE_RANK[best] ? u.type : best),
        "other",
    );

    return {
        id: node.id,
        number: node.number,
        title,
        url: node.url,
        headRefName: branch,
        baseRefName: node.baseRefName,
        createdAt: node.createdAt,
        updatedAt: node.updatedAt,
        isDraft: !!node.isDraft,
        ecosystem,
        directory,
        groupName: titleGroup?.[1] ?? updates.find((u) => u.group)?.group ?? null,
        updates,
        highestUpdateType,
        labels: (node.labels?.nodes ?? []).map((l) => ({ name: l.name, color: l.color })),
        reviewDecision: node.reviewDecision ?? null,
        latestReviews: (node.latestReviews?.nodes ?? []).map((r) => ({ author: r.author?.login ?? "ghost", state: r.state })),
        checks: headCommit?.statusCheckRollup?.state ?? null,
        headOid: headCommit?.oid ?? null,
        mergeable: node.mergeable ?? "UNKNOWN",
        mergeStateStatus: node.mergeStateStatus ?? "UNKNOWN",
        autoMerge: node.autoMergeRequest ? { method: node.autoMergeRequest.mergeMethod } : null,
        files,
        filesTruncated: (node.files?.totalCount ?? files.length) > files.length,
        additions: node.additions ?? 0,
        deletions: node.deletions ?? 0,
        changedFiles: node.changedFiles ?? files.length,
    };
}

export function normalizeAlert(a) {
    return {
        number: a.number,
        url: a.html_url,
        severity: a.security_vulnerability?.severity ?? a.security_advisory?.severity ?? "unknown",
        ghsa: a.security_advisory?.ghsa_id ?? null,
        cve: a.security_advisory?.cve_id ?? null,
        summary: a.security_advisory?.summary ?? "",
        package: a.dependency?.package?.name ?? "",
        ecosystem: normalizeAlertEcosystem(a.dependency?.package?.ecosystem),
        manifestPath: a.dependency?.manifest_path ?? "",
        relationship: a.dependency?.relationship ?? null,
        scope: a.dependency?.scope ?? null,
        vulnerableRange: a.security_vulnerability?.vulnerable_version_range ?? null,
        patched: a.security_vulnerability?.first_patched_version?.identifier ?? null,
        createdAt: a.created_at,
    };
}

// ---------------------------------------------------------------------------
// Derived state

const ANCESTOR_MANIFEST_ECOSYSTEMS = new Set(["nuget", "maven"]);

function prCoversManifest(pr, alert) {
    const alertDir = dirname(alert.manifestPath);
    const allowAncestor = ANCESTOR_MANIFEST_ECOSYSTEMS.has(alert.ecosystem);
    const dirs = new Set(pr.files.map(dirname));
    dirs.add(pr.directory.replace(/^\/+/, "").replace(/\/+$/, ""));
    for (const d of dirs) {
        if (d === alertDir) return true;
        if (allowAncestor && (d === "" || alertDir.startsWith(d + "/"))) return true;
    }
    return pr.files.includes(alert.manifestPath);
}

export function prStatus(pr) {
    if (pr.mergeable === "CONFLICTING" || pr.mergeStateStatus === "DIRTY") return "conflicts";
    if (pr.checks === "FAILURE" || pr.checks === "ERROR") return "failing";
    if (pr.checks === "PENDING" || pr.checks === "EXPECTED") return "pending";
    if (pr.mergeStateStatus === "BEHIND") return "behind";
    if (pr.isDraft) return "draft";
    if (pr.mergeable === "MERGEABLE") {
        if (pr.mergeStateStatus === "BLOCKED" && pr.reviewDecision === "REVIEW_REQUIRED") return "needs-review";
        return "ready";
    }
    return "unknown";
}

export function deriveState({ prs, alerts, reviews }) {
    const depIndex = new Map();
    const fileIndex = new Map();
    for (const pr of prs) {
        for (const u of pr.updates) {
            const key = `${pr.ecosystem}:${u.name.toLowerCase()}`;
            if (!depIndex.has(key)) depIndex.set(key, []);
            const list = depIndex.get(key);
            if (!list.includes(pr)) list.push(pr);
        }
        for (const f of pr.files) {
            if (!fileIndex.has(f)) fileIndex.set(f, []);
            fileIndex.get(f).push(pr.number);
        }
    }

    const prSecurity = new Map(prs.map((p) => [p.number, { fixes: [], related: [] }]));
    const derivedAlerts = (alerts ?? []).map((alert) => {
        const candidates = depIndex.get(`${alert.ecosystem}:${alert.package.toLowerCase()}`) ?? [];
        const fixedBy = [];
        const related = [];
        for (const pr of candidates) {
            const update = pr.updates.find((u) => u.name.toLowerCase() === alert.package.toLowerCase());
            const versionFixes = alert.patched ? compareVersions(update?.to, alert.patched) >= 0 : true;
            if (versionFixes && prCoversManifest(pr, alert)) {
                fixedBy.push(pr.number);
                prSecurity.get(pr.number).fixes.push(alert.number);
            } else {
                related.push(pr.number);
                prSecurity.get(pr.number).related.push(alert.number);
            }
        }
        return { ...alert, fixedBy, relatedPrs: related };
    });

    const outPrs = prs.map((pr) => {
        const sameDependency = new Set();
        for (const u of pr.updates) {
            for (const other of depIndex.get(`${pr.ecosystem}:${u.name.toLowerCase()}`) ?? []) {
                if (other.number !== pr.number) sameDependency.add(other.number);
            }
        }
        const sharedFiles = new Map();
        for (const f of pr.files) {
            for (const n of fileIndex.get(f) ?? []) {
                if (n === pr.number) continue;
                if (!sharedFiles.has(n)) sharedFiles.set(n, []);
                sharedFiles.get(n).push(f);
            }
        }
        const review = reviews?.[pr.number] ?? null;
        return {
            ...pr,
            status: prStatus(pr),
            security: prSecurity.get(pr.number),
            overlaps: {
                sameDependency: [...sameDependency].sort((a, b) => a - b),
                sharedFiles: [...sharedFiles.entries()].map(([number, files]) => ({ number, files })).sort((a, b) => a.number - b.number),
            },
            agentReview: review ? { ...review, stale: !!(review.headOid && pr.headOid && review.headOid !== pr.headOid) } : null,
        };
    });

    const byNumber = new Map(outPrs.map((p) => [p.number, p]));
    const dependencyClusters = [];
    for (const [key, list] of depIndex) {
        if (list.length < 2) continue;
        const [ecosystem, ...rest] = key.split(":");
        const name = list[0].updates.find((u) => `${ecosystem}:${u.name.toLowerCase()}` === key)?.name ?? rest.join(":");
        const targets = [...new Set(list.map((p) => p.updates.find((u) => u.name.toLowerCase() === name.toLowerCase())?.to).filter(Boolean))];
        dependencyClusters.push({
            key,
            name,
            ecosystem,
            targets: targets.sort(compareVersions),
            divergent: targets.length > 1,
            prs: list.map((p) => p.number).sort((a, b) => a - b),
            statuses: countBy(list.map((p) => byNumber.get(p.number).status)),
        });
    }
    dependencyClusters.sort((a, b) => b.prs.length - a.prs.length || a.name.localeCompare(b.name));

    const fileHotspots = [...fileIndex.entries()]
        .filter(([, nums]) => nums.length > 1)
        .map(([path, nums]) => ({ path, prs: [...nums].sort((a, b) => a - b) }))
        .sort((a, b) => b.prs.length - a.prs.length || a.path.localeCompare(b.path));

    return { prs: outPrs, alerts: derivedAlerts, dependencyClusters, fileHotspots };
}

const LOCKFILE_RE = /(^|\/)(package-lock\.json|npm-shrinkwrap\.json|yarn\.lock|pnpm-lock\.yaml|bun\.lockb?|Cargo\.lock|packages\.lock\.json|poetry\.lock|uv\.lock|Gemfile\.lock|composer\.lock|go\.sum|Pipfile\.lock)$/i;

export function isLockfile(path) {
    return LOCKFILE_RE.test(path ?? "");
}

// Parses `git diff` / `gh pr diff` output into files → hunks → lines.
// Lines are kept as raw strings including the leading +/-/space/\ marker.
export function parseUnifiedDiff(text, { maxLines = 20_000, maxLinesPerFile = 4_000 } = {}) {
    const files = [];
    let file = null;
    let hunk = null;
    let totalLines = 0;
    let truncated = false;
    for (let line of (text ?? "").split("\n")) {
        if (line.endsWith("\r")) line = line.slice(0, -1);
        if (line.startsWith("diff --git ")) {
            const m = line.match(/^diff --git "?a\/(.*?)"? "?b\/(.*?)"?$/);
            file = {
                path: m?.[2] ?? line.slice(11),
                oldPath: null,
                status: "modified",
                additions: 0,
                deletions: 0,
                binary: false,
                lockfile: false,
                truncated: false,
                lineCount: 0,
                hunks: [],
            };
            files.push(file);
            hunk = null;
            continue;
        }
        if (!file) continue;
        if (line.startsWith("@@")) {
            const m = line.match(/^@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@/);
            hunk = { header: line, oldStart: Number(m?.[1] ?? 0), newStart: Number(m?.[2] ?? 0), lines: [] };
            file.hunks.push(hunk);
            continue;
        }
        if (!hunk) {
            if (line.startsWith("new file mode")) file.status = "added";
            else if (line.startsWith("deleted file mode")) file.status = "removed";
            else if (line.startsWith("rename from ")) {
                file.status = "renamed";
                file.oldPath = line.slice(12);
            } else if (line.startsWith("rename to ")) file.path = line.slice(10);
            else if (line.startsWith("Binary files ")) file.binary = true;
            continue;
        }
        const marker = line[0];
        if (marker === "+") file.additions++;
        else if (marker === "-") file.deletions++;
        else if (marker !== " " && marker !== "\\") continue;
        if (file.lineCount >= maxLinesPerFile || totalLines >= maxLines) {
            file.truncated = true;
            if (totalLines >= maxLines) truncated = true;
            continue;
        }
        hunk.lines.push(line);
        file.lineCount++;
        totalLines++;
    }
    for (const f of files) f.lockfile = isLockfile(f.path);
    return {
        files,
        truncated,
        totalLines,
        additions: files.reduce((s, f) => s + f.additions, 0),
        deletions: files.reduce((s, f) => s + f.deletions, 0),
    };
}

// Re-serialises a parsed diff as unified text for the agent.
export function formatDiff(diff, { path, maxLines = 400, includeLockfiles = false } = {}) {
    const out = [];
    let budget = maxLines;
    const omitted = [];
    for (const f of diff.files) {
        if (path && f.path !== path) continue;
        const summary = `${f.path} (${f.status}, +${f.additions} −${f.deletions})`;
        if (!path && f.lockfile && !includeLockfiles) {
            omitted.push(`${summary} — lockfile`);
            continue;
        }
        if (f.binary) {
            omitted.push(`${summary} — binary`);
            continue;
        }
        if (budget <= 0) {
            omitted.push(`${summary} — line budget exhausted`);
            continue;
        }
        out.push(`diff --git a/${f.oldPath ?? f.path} b/${f.path}`);
        for (const hk of f.hunks) {
            if (budget <= 0) break;
            out.push(hk.header);
            for (const l of hk.lines) {
                if (budget-- <= 0) {
                    out.push("… (truncated)");
                    break;
                }
                out.push(l);
            }
        }
        if (f.truncated) out.push("… (file diff truncated by the canvas)");
    }
    return { text: out.join("\n"), omitted };
}

export function countBy(values) {
    const out = {};
    for (const v of values) out[v] = (out[v] ?? 0) + 1;
    return out;
}
