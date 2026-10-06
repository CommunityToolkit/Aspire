---
name: GitHub workflows
description: Use when modifying GitHub Actions or GitHub Agentic Workflow files.
applyTo: ".github/workflows/**"
---

# Workflow guidance

- Use the minimum required permissions.
- Keep normal GitHub Actions YAML changes consistent with nearby workflows and pinned action versions.
- Files such as `aspire-upgrade.md` are GitHub Agentic Workflow sources; their corresponding `*.lock.yml` files are generated.
- Modify the Markdown source rather than manually editing a generated agentic workflow lock file.
- When changing an agentic workflow or its gh-aw version, compile and validate the workflow with the repository-supported gh-aw commands.
- The reusable integration-test matrix is `.github/workflows/tests.yaml`.
