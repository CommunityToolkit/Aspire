---
name: Generated API baselines
description: Protect generated public API baseline files from manual edits.
applyTo: "**/api/*.cs"
---

# Generated API baseline guidance

Files under `*/api/*.cs` are generated public API baselines.

- Do not create or edit these files manually.
- Change the source API first.
- Regenerate baselines only when the task explicitly requires an API review or baseline update.
- Review generated changes for accidental public API additions, removals, or signature changes.
