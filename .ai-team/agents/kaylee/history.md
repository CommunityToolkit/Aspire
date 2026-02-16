# Project Context

- **Owner:** Aaron Powell (me@aaron-powell.com)
- **Project:** Aspire Community Toolkit — community-driven integrations and extensions for .NET Aspire
- **Stack:** C#, .NET 9, Aspire, xUnit, NuGet, MSBuild, GitHub Actions
- **Created:** 2026-02-13

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **Installer resource pattern:** Language integrations that need pre-app tooling (Bun → `BunInstallerResource`, Go → `GoModInstallerResource`, Rust → `RustToolInstallerResource`) follow a consistent pattern: create a child `ExecutableResource`, set it as a parent relationship, exclude from manifest, and use `WaitForCompletion` to block the main app.
- **`WithCommand()` from Aspire 13:** `ExecutableResourceBuilderExtensions.WithCommand<T>(IResourceBuilder<T>, string)` swaps the executable on a resource builder. Used by McpInspector, JavaScript.Extensions, and now Rust for alternative tool support.
- **Arg clearing for command swaps:** When replacing a command via `WithCommand()`, existing args (e.g., `["run"]` from `AddRustApp`) must be explicitly cleared using `context.Args.Clear()` inside a `WithArgs` callback, or the old args leak through.
- **Resource constructor flexibility:** `RustAppExecutableResource` uses an optional `command` parameter with default `"cargo"` — simpler than constructor overloading. Consistent with how `ExecutableResource` works.
- **Test patterns for installers:** Verify installer resources via `appModel.Resources.OfType<T>()`, check `WaitAnnotation` linkage with `TryGetAnnotationsOfType<WaitAnnotation>`, and validate args with `GetArgumentListAsync()` (from `CommunityToolkit.Aspire.Testing`).
- **Key paths:** `src/CommunityToolkit.Aspire.Hosting.Rust/` contains `RustAppExecutableResource.cs`, `RustToolInstallerResource.cs`, `RustAppHostingExtension.cs`. Tests at `tests/CommunityToolkit.Aspire.Hosting.Rust.Tests/`.

- Rust integration lives in `src/CommunityToolkit.Aspire.Hosting.Rust/` with `RustAppExecutableResource`, `RustAppHostingExtension`, and new `RustToolInstallerResource`
- Aspire's `.WithCommand()` API changes the executable for an `ExecutableResource` — used by JS extensions, MCP inspector, and now Rust
- The Bun integration's `BunInstallerResource` pattern (installer resource + `WaitForCompletion` + `WithParentRelationship` + `ExcludeFromManifest`) is the canonical pattern for pre-run tool installation
- Tests in `tests/CommunityToolkit.Aspire.Hosting.Rust.Tests/` — `AddRustAppTests` for functional tests, `RustAppPublicApiTests` for null-guard tests
- `WithArgs(context => { context.Args.Clear(); ... })` is the pattern for replacing previously-configured args on a resource
- `cargo install` convention: flags before package name (e.g., `cargo install --version 0.2.0 --locked package-name`)
