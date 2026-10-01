# CommunityToolkit Aspire contributor guidance

This repository contains independently shipped Aspire integrations and extensions. Most changes should stay within one integration and its matching tests, examples, and README.

## Repository map

- `src/` contains shipping packages.
  - Hosting packages use `CommunityToolkit.Aspire.Hosting.*`.
  - Client packages use `CommunityToolkit.Aspire.*`.
- `tests/` contains the matching xUnit projects.
- `examples/` contains minimal AppHosts and consumer examples.
- `docs/` contains contributor and maintenance documentation.
- `eng/` contains repository automation.

## Working in this repository

1. Identify the integration under change.
2. Read its adjacent tests, example, and README.
3. Compare with one or two nearby integrations that use the same pattern.
4. Use the relevant skill under `.agents/skills/` for specialized workflows.
5. Run the narrowest build or test command that proves the change.

Follow applicable path-specific guidance in `.github/instructions/`. Keep generated `*/api/*.cs` files unchanged unless the task explicitly requires API baseline regeneration.

## Core conventions

- Hosting extension methods use the `Aspire.Hosting` namespace.
- Client extension methods use the `Microsoft.Extensions.Hosting` namespace.
- Hosting resource types normally use `Aspire.Hosting.ApplicationModel`.
- Public APIs require XML documentation.
- The repository treats warnings as errors.
- Prefer focused changes and do not refactor unrelated integrations.

Use `.github/copilot-instructions.md` for the repository-wide Copilot baseline and `docs/create-integration.md` when adding an integration.
