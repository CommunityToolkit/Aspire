## .NET Aspire Community Toolkit — Copilot Coding Instructions

### Project Overview

-   This repo is a collection of community-driven integrations and extensions for [.NET Aspire](https://aka.ms/dotnet/aspire), supporting a wide range of runtimes and cloud-native patterns.
-   Major components are organized by integration in `src/`, with tests in `tests/` and usage examples in `examples/`.
-   Each integration is a NuGet package, following naming: `CommunityToolkit.Aspire.Hosting.*` (hosting) or `CommunityToolkit.Aspire.*` (client).

### Architecture & Patterns

-   **Resource-based model:** Integrations define resources (e.g., `NodeAppResource`, `NpmInstallerResource`) that appear in the Aspire dashboard and support parent-child relationships for startup ordering and dependency management.
-   **Extension methods:**
    -   Hosting integrations: extension methods in `Aspire.Hosting` namespace.
    -   Client integrations: extension methods in `Microsoft.Extensions.Hosting` namespace.
    -   Use file-scoped namespaces.
-   **Installer resources:** For Node.js, package installers (npm/yarn/pnpm) are modeled as `ExecutableResource` instances, providing dashboard visibility and proper process management. See `src/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions/REFACTORING_NOTES.md` for rationale and migration.

### Coding Style

-   Use spaces for indentation (4 per level; 2 for `.csproj`).
-   All public members require XML doc comments.
-   Prefer explicit type declarations unless type is obvious.
-   Use collection initializers: `List<T> items = []`.
-   Use `is not null`/`is null` over `!= null`/`== null`.

#### Example: Hosting Integration

```csharp
namespace Aspire.Hosting;

public static class SomeProgramExtensions
{
    /// <summary>Adds a SomeProgram resource.</summary>
    public static IResourceBuilder<SomeProgramResource> AddSomeProgram(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name)
    {
        SomeProgramResource resource = new(name);
        return builder.AddResource(resource);
    }
}
```

### Developer Workflows

-   **Build:** Use `dotnet build` or VS Code task `build`.
-   **Test:**
    -   All tests use xUnit. Place test projects in `tests/` (naming: `CommunityToolkit.Aspire.Hosting.MyIntegration.Tests`).
    -   Integration tests that test containerized resources will require Docker; mark with `[RequiresDocker]` if so.
    -   Use `WaitForTextAsync` to wait for resource readiness if not using Aspire health checks.
    -   Add new test projects to CI by running `./eng/testing/generate-test-list-for-workflow.sh` and updating `.github/workflows/tests.yml`.
-   **Debug:** For devcontainers, manually forward DCP ports if HTTP endpoints are unreachable.

### Project Conventions

-   Integrations must add `Aspire.Hosting` as a dependency; see `Directory.Build.props` for shared MSBuild config.
-   Use the [create-integration guide](../docs/create-integration.md) for new integrations.
-   For Azure/Dapr integrations, see `src/Shared/DaprAzureExtensions/README.md` for shared resource patterns.

### External Dependencies & Integration

-   Many integrations wrap external services (e.g., Dapr, MinIO, k6, Node.js, Python, Rust, Java, etc.).
-   Each integration's README in `src/` or `examples/` details usage, supported versions, and special setup.
-   For Node.js, package manager flags can be customized via extension methods (see `src/CommunityToolkit.Aspire.Hosting.NodeJS.Extensions/README.md`).

### Documentation & Contribution

-   Main docs: [Microsoft Docs](https://learn.microsoft.com/dotnet/aspire/community-toolkit/overview)
-   FAQ: `docs/faq.md` | Contribution: `CONTRIBUTING.md`
-   Versioning: `docs/versioning.md` | Diagnostics: `docs/diagnostics.md`

---

If you find any unclear or incomplete sections, please provide feedback to improve these instructions.
