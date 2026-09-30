# Aspire 13.5 Breaking Changes - Agent Reference

Use this reference when upgrading AppHosts or reviewing generated code, scripts, and CI
for Aspire 13.5. Sources:

- [Aspire 13.5 release notes](https://aspire.dev/whats-new/aspire-13-5/)
- [Aspire 13.5.1](https://github.com/microsoft/aspire/releases/tag/v13.5.1)
- [Aspire 13.5.2](https://github.com/microsoft/aspire/releases/tag/v13.5.2)
- [Aspire 13.5.3](https://github.com/microsoft/aspire/releases/tag/v13.5.3)

## Package compatibility for 13.5 migrations

- For a migration into the 13.5 family, select compatible SDK and hosting packages
  for the chosen servicing release.
- Preview-only integrations use compatible preview packages instead of nonexistent
  stable versions. Use integration discovery and package-specific docs to select a
  build compatible with the chosen SDK; do not assume identical version strings.
- Do not mix 13.4 and 13.5 SDK or hosting integration packages. Mixed graphs can fail at
  startup with `MissingMethodException` or `TypeLoadException`, especially for Kubernetes,
  Azure Functions, Go, JavaScript, and Python integrations.
- When updating, update the CLI first, then update every Aspire package together:

  ```bash
  aspire update --self
  aspire update --yes --non-interactive
  ```

  Package-manager-owned CLI installs print their own update command instead of overwriting
  the managed installation.

## Breaking-change scrub

| Stale surface | Aspire 13.5 migration |
|---------------|-----------------------|
| Hosting callback context `.ServiceProvider` | Use `.Services`. |
| `PublishAsConnectionString(...)` | Use `AddConnectionString(...)` in publish-mode app model code. |
| `aspire ps --resources` | Use `aspire describe` for resource state and details. |
| `aspire ps --include-hidden` | Use `aspire describe --include-hidden` for hidden resources. `--include-hidden` on `aspire resource` exposes hidden resource commands, not hidden resources. |
| `Aspire.Hosting.GitHub.Models` | Migrate to Azure AI Foundry. GitHub Models is deprecated and omitted from `aspire add` discovery. |
| `TerminalOptions.Shell` | Remove it. `Columns` and `Rows` must be positive. |
| `DevTunnelRegion.UkSouth` / `SouthEastAsia` | Use `UKSouth` / `SoutheastAsia`. |
| Go export with one optional `options` DTO | Pass the DTO directly after regenerating the SDK; do not use the old generated method-options wrapper. |
| Dashboard AI Assistant | Removed. Use agent skills and the AppHost/CLI integration instead. |
| Assuming VS Code auto-opens the dashboard | Opt in with the `dashboardBrowser` setting or `launch.json`, or open the in-editor dashboard. |
| `OrleansProviderTypeAnnotation` / `ProviderConfiguration` | Internal in 13.5; do not generate direct references. |
| Old `DotnetProjectResource` namespace | Use `Aspire.Hosting.Dotnet`; `AddDotnetProject` is experimental under `ASPIREDOTNETPROJECT001`. |

Proxyless endpoints without an explicit public port now receive one during service
preparation. The default allocation range is `10000-32767`; override it only when required
with `ASPIRE_PROXYLESS_ENDPOINT_PORT_RANGE=start-end`.

## TypeScript AppHosts

- Current entry points are `apphost.mts`. `apphost.ts` is a legacy entry point that should
  still be detected.
- `aspire update --migrate` updates the project's Aspire packages first, then migrates
  legacy `apphost.ts`, configuration, TypeScript configuration, and generated imports.
  Run `aspire update --migrate --yes --non-interactive` only after the user approves both
  the package update and migration. `aspire-orchestration` owns this CLI migration;
  `aspireify` owns only any AppHost source authoring that remains afterward.
- TypeScript AppHosts are generally available. Remove stale `ASPIREATS001` suppressions.
- Continue importing generated APIs from `./.aspire/modules/aspire.mjs`; never edit
  `.aspire/modules/` directly.
- 13.5 adds TypeScript custom health-check callbacks, container file copying, HTTPS
  developer certificates, resource command arguments, and closer Interaction Service parity.

## AppHost interactions and commands

- Resource command arguments are stable. Define `CommandOptions.Arguments`, then read
  `ExecuteCommandContext.Arguments` in C# or `await context.arguments()` in TypeScript.
  The dashboard renders input controls and the CLI exposes named `--<name>` options.
- Prefer command arguments when input must work from both dashboard and CLI. Direct
  Interaction Service prompts require an attached UI; check `IInteractionService.IsAvailable`
  in C#. In TypeScript, call
  `const interaction = await context.services().getInteractionService()`, then check
  `await interaction.isAvailable()`. Provide a noninteractive path before prompting.
- Interaction inputs and file uploads are stable. C# reads uploaded `InteractionFile`
  content; TypeScript receives an on-disk `filePath`. Progress dialogs remain experimental
  under `ASPIREINTERACTION001`.
- `WithTerminal()` and `aspire terminal` are experimental. C# callers suppress
  `ASPIRETERMINAL001`; enable CLI commands with:

  ```bash
  aspire config set features.terminalCommandsEnabled true
  ```

  Do not invent a `TerminalOptions.Shell` property.

## CLI and lifecycle

- `aspire ps` lists running AppHosts, process IDs, logs, and dashboard URLs.
- `aspire describe [resource]` lists resource state, health, and endpoints. Use
  `--include-hidden` there when hidden resources are needed.
- `aspire resources` remains a compatibility alias for `aspire describe`; prefer the primary
  name in new guidance.
- `aspire stop --force` performs a normal stop and then permanently deletes persistent
  resource instances without another confirmation. Never use it unless the user explicitly
  requests that data-destructive cleanup for one exact AppHost.
- New C# templates enable `AspireUseCliBundle=true`. Existing projects remain opt-in. The
  bundle lets `dotnet run` delegate through Aspire, but agents should still use
  `aspire start --non-interactive` for detached, exact-target lifecycle control.
- The CLI can be installed from npm (`npm install -g @microsoft/aspire-cli`) and Nix in
  addition to the install scripts, WinGet, Homebrew, mise, and the .NET global tool.

## Deployment additions

- Kubernetes and AKS support first-class persistent volumes through
  `AddPersistentVolume`, `WithStorageClass`, `WithCapacity`, `WithAccessMode`, and
  `WithPersistentVolume`. Bound workloads render as StatefulSets. These APIs are
  experimental under `ASPIRECOMPUTE002`.
- Azure Container Apps supports `WithUniqueResourceNaming()` for multiple environments in
  one resource group. It is experimental (`ASPIREACANAMING002`) and can recreate an existing
  environment, so do not apply it retroactively without explicit approval.
- Azure resources can reference existing resources across resource groups, subscriptions,
  and tenants through `AsExistingInResourceGroup`, `AsExistingInSubscription`, and
  `AsExistingInTenant` (plus run/publish variants).
- Azure Container Apps and App Service environments support delegated subnets.
- `Aspire.Hosting.Radius` adds a preview Radius target with `AddRadiusEnvironment` and
  `WithNamespace`. Verify current API docs and experimental diagnostics before editing.
- Docker Compose gains shared-memory customization and Blazor gateway publishing.
- Redis 8 modules are available through `WithModule` and `RedisModules` constants for
  JSON, Search, Bloom Filter, and TimeSeries.
- Foundry Local uses the installed `foundry` CLI, and executable/container resources can
  publish as hosted agents with `AsHostedAgent`.

## Patch-level fixes

- **13.5.1** restores 13.5 TypeScript/Java code generation compatibility with older 13.4
  CLIs and fixes polyglot AppHost startup on macOS.
- **13.5.2** removes an unused Windows CLI helper binary; no AppHost guidance change.
- **13.5.3** fixes dashboard graph crashes for multi-path resource icons and restores
  DevTunnel public URLs in dashboard and MCP resource snapshots.
