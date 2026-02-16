---
name: "aspire-installer-resources"
description: "Pattern for creating pre-app installer resources in Aspire hosting integrations"
domain: "api-design"
confidence: "medium"
source: "earned"
---

## Context
When a language integration needs to install tools, download dependencies, or run setup commands before the main app starts, it uses an installer resource pattern. This applies to any Aspire hosting integration that wraps a language runtime (Bun, Go, Rust, Python, Node.js) and needs prerequisite steps.

## Patterns

### Installer Resource Class
Create a minimal `ExecutableResource` subclass in `Aspire.Hosting.ApplicationModel` namespace:
```csharp
public class MyToolInstallerResource(string name, string workingDirectory)
    : ExecutableResource(name, "my-tool", workingDirectory);
```

### Extension Method Structure
The installer extension method follows this exact sequence:
1. Guard clauses (`ArgumentNullException.ThrowIfNull`)
2. Skip in publish mode (`!builder.ApplicationBuilder.ExecutionContext.IsPublishMode`)
3. Create installer resource with descriptive name (`$"{builder.Resource.Name}-my-install-{package}"`)
4. Configure via builder chain: `.WithArgs(...)` → `.WithParentRelationship(builder.Resource)` → `.ExcludeFromManifest()`
5. Link dependency: `builder.WaitForCompletion(installerBuilder)`

### Command Replacement
Use Aspire's `ExecutableResourceBuilderExtensions.WithCommand<T>(builder, command)` to swap the executable. Clear existing args with `context.Args.Clear()` inside a `WithArgs` callback when the new command expects different arguments.

## Examples
- `BunInstallerResource` + `WithBunPackageInstallation` (Bun)
- `GoModInstallerResource` + `WithGoModTidy`/`WithGoModDownload` (Go)
- `RustToolInstallerResource` + `WithCargoInstall` (Rust)

## Anti-Patterns
- **Forgetting `ExcludeFromManifest()`** — installer resources should not appear in the deployment manifest.
- **Missing `WithParentRelationship()`** — breaks the Aspire dashboard hierarchy and startup ordering.
- **Not gating on `IsPublishMode`** — installers should only run during development, not when publishing.
- **Not clearing args before command swap** — old args (e.g., `["run"]`) leak into the new command's argument list.
