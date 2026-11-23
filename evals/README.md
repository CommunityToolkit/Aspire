# Skill evaluations

This [GitHub Copilot Evals](https://aka.ms/evals) smoke suite checks specific skill behavior: client configuration precedence and keyed health checks, hosting dependencies for a local one-shot migration, and TypeScript AppHost startup coverage. Each trial stages only the targeted skill (including its reference files) and uses a hypothetical integration. The cases check skill activation, specific review or planning outcomes, and that the workspace stays unchanged. They do not build, start, or deploy an Aspire app.

Add a case to `evals/<skill>/eval.yaml` using the [Vally eval spec](https://microsoft.github.io/vally/reference/eval-spec/), or add an eval in that layout for another skill. Paths in `agent_environment.skills` are relative to the eval file. Prefer built-in graders and focused prompts with observable outcomes.

## Run locally

From the repository root, with Node.js 24+ and Git:

```shell
npx --yes --package=@microsoft/vally-cli@0.21.0 vally lint --eval-spec .
```

Lint requires no model credential. To also run live trials, follow [Vally's Copilot runtime setup](https://microsoft.github.io/vally/get-started/runtime-setup/) and authenticate with a Copilot account that has access to the configured model. Only run evals from a checkout you trust.

```shell
npx --yes --package=@microsoft/vally-cli@0.21.0 vally eval --suite smoke --workers 1 --max-retries 0 --require-pass
```

Vally writes each local run to a timestamped subdirectory of `vally-results/` at the repository root. Git ignores this directory.

## Run in CI

The [Skill evals workflow](../.github/workflows/skill-evals.yml) runs on same-repository PRs and pushes to `main` affecting skills, evals, or their configuration. Maintainers can also dispatch it on a trusted ref. Fork and Dependabot PRs skip both jobs: even native lint can load executable plugins, so CI does not evaluate untrusted PR content with a privileged token. Fork contributions are evaluated after they land on `main`; the live job is not suitable as a required pre-merge check for forks.

The organization must allow `github/evals-action` and enable **Allow use of Copilot CLI billed to the organization**. The live job uses only the automatic workflow token with `copilot-requests: write`; no PAT or repository secret is needed. The configured Copilot model must be available to that organization. Live trials consume Copilot AI credits; CI runs each case once, with one worker and no retries. Failed cases and runtime/authentication errors fail the check. See the Actions job summary and structured result artifact for details; raw transcripts are not uploaded.
