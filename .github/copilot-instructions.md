## Aspire Community Toolkit

This repository contains community-maintained Aspire integrations and extensions. Shipping packages are under `src/`, matching xUnit projects are under `tests/`, and minimal usage examples are under `examples/`.

### Package and API conventions

- Hosting packages use `CommunityToolkit.Aspire.Hosting.*`; client packages use `CommunityToolkit.Aspire.*`.
- Hosting extension methods use the `Aspire.Hosting` namespace.
- Client extension methods use the `Microsoft.Extensions.Hosting` namespace.
- Hosting resource types normally use `Aspire.Hosting.ApplicationModel`.
- Use file-scoped namespaces and the repository's existing formatting.
- All public APIs require XML documentation.
- Validate public inputs with the established .NET throw helpers used by nearby integrations.
- Prefer `is null` and `is not null` over equality checks with `null`.
- Do not manually edit generated `*/api/*.cs` files unless API baseline regeneration is explicitly required.

### Working approach

- Start with the integration being changed, then inspect its tests, example, README, and one or two same-pattern integrations.
- Use the applicable files under `.github/instructions/` for path-specific guidance.
- Use focused workflows under `.agents/skills/` for specialized tasks.
- Keep changes scoped to the requested integration and avoid unrelated refactoring.
- Run the narrowest relevant build or test command. The repository treats warnings as errors.

### Key references

- [Creating an integration](../docs/create-integration.md)
- [Development environment](../docs/setup.md)
- [Contributing](../CONTRIBUTING.md)
- [Diagnostics](../docs/diagnostics.md)
- [Versioning](../docs/versioning.md)
