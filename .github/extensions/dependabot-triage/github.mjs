// Thin wrappers over the GitHub CLI (`gh`). Uses the user's existing gh auth.

import { spawn } from "node:child_process";

export class GhError extends Error {
    constructor(message, details = {}) {
        super(message);
        this.name = "GhError";
        this.stdout = details.stdout;
        this.stderr = details.stderr;
        this.exitCode = details.exitCode;
    }
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

export function runGh(args, { input, cwd, timeoutMs = 120_000 } = {}) {
    return new Promise((resolve, reject) => {
        const child = spawn("gh", args, {
            cwd,
            windowsHide: true,
            env: { ...process.env, GH_PROMPT_DISABLED: "1", NO_COLOR: "1", GH_NO_UPDATE_NOTIFIER: "1", GH_PAGER: "" },
        });
        const out = [];
        const err = [];
        child.stdout.on("data", (d) => out.push(d));
        child.stderr.on("data", (d) => err.push(d));
        const timer = setTimeout(() => child.kill(), timeoutMs);
        child.on("error", (e) => {
            clearTimeout(timer);
            reject(new GhError(e.code === "ENOENT" ? "GitHub CLI (gh) was not found on PATH." : e.message));
        });
        child.on("close", (code) => {
            clearTimeout(timer);
            const stdout = Buffer.concat(out).toString("utf8");
            const stderr = Buffer.concat(err).toString("utf8");
            if (code === 0) resolve(stdout);
            else reject(new GhError((stderr || stdout || `gh exited with code ${code}`).trim(), { stdout, stderr, exitCode: code }));
        });
        child.stdin.on("error", () => {});
        child.stdin.end(input ?? "");
    });
}

const TRANSIENT = /\b50[234]\b|timeout|couldn't respond|ECONNRESET|something went wrong|timed out/i;

export async function graphql(query, variables = {}, opts = {}) {
    const body = JSON.stringify({ query, variables });
    let lastErr;
    for (let attempt = 0; attempt < 3; attempt++) {
        try {
            let text;
            try {
                text = await runGh(["api", "graphql", "--input", "-"], { ...opts, input: body });
            } catch (e) {
                // gh exits non-zero for partial GraphQL errors but still prints the payload.
                const parsed = tryParse(e.stdout);
                if (parsed?.data) return parsed.data;
                throw e;
            }
            const json = JSON.parse(text);
            if (json.errors?.length && !json.data) throw new GhError(json.errors.map((x) => x.message).join("; "));
            return json.data;
        } catch (e) {
            lastErr = e;
            if (!TRANSIENT.test(e.message)) throw e;
            await sleep(1000 * (attempt + 1));
        }
    }
    throw lastErr;
}

function tryParse(text) {
    try {
        return text ? JSON.parse(text) : null;
    } catch {
        return null;
    }
}

function splitRepo(repo) {
    const [owner, name] = repo.split("/");
    return { owner, name };
}

export async function detectRepo(cwd) {
    const out = await runGh(["repo", "view", "--json", "nameWithOwner", "-q", ".nameWithOwner"], { cwd, timeoutMs: 30_000 });
    return out.trim();
}

export async function fetchRepoInfo(repo) {
    const data = await graphql(
        `query($owner:String!,$name:String!){
            viewer{ login }
            repository(owner:$owner,name:$name){
                nameWithOwner url viewerPermission
                autoMergeAllowed squashMergeAllowed mergeCommitAllowed rebaseMergeAllowed deleteBranchOnMerge
                defaultBranchRef{ name }
            }
        }`,
        splitRepo(repo),
    );
    const r = data.repository;
    return {
        nameWithOwner: r.nameWithOwner,
        url: r.url,
        viewer: data.viewer?.login ?? null,
        viewerPermission: r.viewerPermission,
        canWrite: ["ADMIN", "MAINTAIN", "WRITE"].includes(r.viewerPermission),
        autoMergeAllowed: r.autoMergeAllowed,
        mergeMethods: [
            r.squashMergeAllowed && "squash",
            r.mergeCommitAllowed && "merge",
            r.rebaseMergeAllowed && "rebase",
        ].filter(Boolean),
        deleteBranchOnMerge: r.deleteBranchOnMerge,
        defaultBranch: r.defaultBranchRef?.name ?? null,
    };
}

// `mergeable` is intentionally excluded: GitHub computes it lazily and it makes
// search pages 3-4x slower. It is backfilled via fetchMergeability().
const LIST_QUERY = `query($q:String!,$cursor:String){
    search(type:ISSUE, query:$q, first:50, after:$cursor){
        issueCount
        pageInfo{ hasNextPage endCursor }
        nodes{ ... on PullRequest {
            id number title url headRefName baseRefName createdAt updatedAt isDraft reviewDecision
            additions deletions changedFiles
            labels(first:15){ nodes{ name color } }
            autoMergeRequest{ mergeMethod }
            latestReviews(first:5){ nodes{ author{ login } state } }
            files(first:100){ totalCount pageInfo{ hasNextPage endCursor } nodes{ path } }
            first: commits(first:1){ nodes{ commit{ message } } }
            head: commits(last:1){ nodes{ commit{ oid statusCheckRollup{ state } } } }
        } }
    }
}`;

export async function listDependabotPrs(repo, onPage) {
    const q = `repo:${repo} is:pr is:open author:app/dependabot sort:created-desc`;
    let cursor = null;
    const all = [];
    for (;;) {
        const data = await graphql(LIST_QUERY, { q, cursor });
        const page = (data.search.nodes ?? []).filter((n) => n && n.number);
        await Promise.all(page.filter((pr) => pr.files?.pageInfo?.hasNextPage).map((pr) => completePrFiles(repo, pr)));
        all.push(...page);
        onPage?.(page, { loaded: all.length, total: data.search.issueCount });
        if (!data.search.pageInfo.hasNextPage) break;
        cursor = data.search.pageInfo.endCursor;
    }
    return all;
}

export async function fetchMergeability(ids) {
    const data = await graphql(
        `query($ids:[ID!]!){ nodes(ids:$ids){ ... on PullRequest { id number state mergeable mergeStateStatus reviewDecision } } }`,
        { ids },
    );
    return (data.nodes ?? []).filter(Boolean);
}

export async function fetchPrDetails(repo, number) {
    const data = await graphql(
        `query($owner:String!,$name:String!,$number:Int!){
            repository(owner:$owner,name:$name){
                pullRequest(number:$number){
                    number state bodyHTML mergeable mergeStateStatus reviewDecision
                    autoMergeRequest{ mergeMethod }
                    files(first:100){ totalCount pageInfo{ hasNextPage endCursor } nodes{ path additions deletions } }
                    head: commits(last:1){ nodes{ commit{ oid statusCheckRollup{ state
                        contexts(first:100){ totalCount nodes{
                            __typename
                            ... on CheckRun { name conclusion status detailsUrl isRequired(pullRequestNumber:$number) checkSuite{ workflowRun{ workflow{ name } } } }
                            ... on StatusContext { context state targetUrl description isRequired(pullRequestNumber:$number) }
                        } }
                    } } } }
                    comments(last:10){ nodes{ author{ login } bodyText createdAt url } }
                    reviews(last:10){ nodes{ author{ login } state submittedAt } }
                }
            }
        }`,
        { ...splitRepo(repo), number },
    );
    const pr = data.repository.pullRequest;
    const files = await fetchAllPrFiles(repo, number, pr.files);
    const commit = pr.head?.nodes?.[0]?.commit;
    const checks = (commit?.statusCheckRollup?.contexts?.nodes ?? []).map((c) =>
        c.__typename === "CheckRun"
            ? {
                  name: c.name,
                  workflow: c.checkSuite?.workflowRun?.workflow?.name ?? null,
                  state: c.status === "COMPLETED" ? c.conclusion : c.status,
                  url: c.detailsUrl,
                  required: !!c.isRequired,
              }
            : { name: c.context, workflow: null, state: c.state, url: c.targetUrl, description: c.description, required: !!c.isRequired },
    );
    return {
        number: pr.number,
        state: pr.state,
        bodyHTML: pr.bodyHTML,
        mergeable: pr.mergeable,
        mergeStateStatus: pr.mergeStateStatus,
        reviewDecision: pr.reviewDecision,
        autoMerge: pr.autoMergeRequest ? { method: pr.autoMergeRequest.mergeMethod } : null,
        headOid: commit?.oid ?? null,
        checksState: commit?.statusCheckRollup?.state ?? null,
        checks,
        files: files.nodes.map((f) => ({ path: f.path, additions: f.additions, deletions: f.deletions })),
        filesTruncated: files.incomplete || files.totalCount > files.nodes.length,
        comments: (pr.comments?.nodes ?? []).map((c) => ({ author: c.author?.login ?? "ghost", body: c.bodyText, createdAt: c.createdAt, url: c.url })),
        reviews: (pr.reviews?.nodes ?? []).map((r) => ({ author: r.author?.login ?? "ghost", state: r.state, submittedAt: r.submittedAt })),
        fetchedAt: new Date().toISOString(),
    };
}

async function completePrFiles(repo, pr) {
    const files = await fetchAllPrFiles(repo, pr.number, pr.files);
    pr.files = {
        ...pr.files,
        totalCount: files.totalCount,
        nodes: files.nodes,
        pageInfo: { hasNextPage: files.incomplete, endCursor: null },
        incomplete: files.incomplete,
    };
}

async function fetchAllPrFiles(repo, number, firstPage) {
    const nodes = [...(firstPage?.nodes ?? [])];
    const totalCount = firstPage?.totalCount ?? nodes.length;
    let pageInfo = firstPage?.pageInfo ?? null;
    let incomplete = false;
    while (pageInfo?.hasNextPage) {
        try {
            const data = await graphql(
                `query($owner:String!,$name:String!,$number:Int!,$cursor:String){
                    repository(owner:$owner,name:$name){
                        pullRequest(number:$number){
                            files(first:100, after:$cursor){
                                totalCount
                                pageInfo{ hasNextPage endCursor }
                                nodes{ path additions deletions }
                            }
                        }
                    }
                }`,
                { ...splitRepo(repo), number, cursor: pageInfo.endCursor },
            );
            const page = data.repository.pullRequest.files;
            nodes.push(...(page.nodes ?? []));
            pageInfo = page.pageInfo;
        } catch {
            incomplete = true;
            break;
        }
    }
    return { nodes, totalCount, incomplete: incomplete || totalCount > nodes.length };
}

export async function fetchPrDiff(repo, number) {
    try {
        return await runGh(["pr", "diff", String(number), "-R", repo, "--color", "never"], { timeoutMs: 90_000 });
    } catch (e) {
        if (/406|too large|exceeded the maximum/i.test(e.message)) {
            throw new GhError("GitHub won't return this diff because it is too large. Open it on GitHub instead.", { stderr: e.stderr });
        }
        throw e;
    }
}

export async function fetchPrState(repo, number) {
    const out = await runGh(["pr", "view", String(number), "-R", repo, "--json", "number,state,url,title,isDraft,mergedAt,headRefName"]);
    return JSON.parse(out);
}

export async function fetchOpenAlerts(repo) {
    const out = await runGh(["api", "--paginate", "--slurp", `repos/${repo}/dependabot/alerts?state=open&per_page=100`], { timeoutMs: 120_000 });
    const pages = JSON.parse(out);
    return pages.flat();
}

// --- Mutations ------------------------------------------------------------

export function approvePr(repo, number, body) {
    const args = ["pr", "review", String(number), "-R", repo, "--approve"];
    if (body) args.push("--body", body);
    return runGh(args);
}

export function mergePr(repo, number, { method = "squash", auto = false, deleteBranch = false, headOid } = {}) {
    const args = ["pr", "merge", String(number), "-R", repo, `--${method}`];
    if (auto) args.push("--auto");
    if (deleteBranch) args.push("--delete-branch");
    if (headOid) args.push("--match-head-commit", headOid);
    return runGh(args);
}

export function disableAutoMerge(repo, number) {
    return runGh(["pr", "merge", String(number), "-R", repo, "--disable-auto"]);
}

export function commentOnPr(repo, number, body) {
    return runGh(["pr", "comment", String(number), "-R", repo, "--body", body]);
}

export function closePr(repo, number, comment) {
    const args = ["pr", "close", String(number), "-R", repo];
    if (comment) args.push("--comment", comment);
    return runGh(args);
}
