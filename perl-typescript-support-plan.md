# Perl TypeScript AppHost Support Plan

## Goal

Enable `CommunityToolkit.Aspire.Hosting.Perl` to work from a TypeScript AppHost, using the current `examples/perl/cpanm-api-integration` sample as the first validated scenario.

## What The Aspire Docs Say

- `aspire docs get multi-language-integrations`:
  - TypeScript AppHost support comes from ATS annotations on the .NET hosting integration.
  - The CLI scans `[AspireExport]` methods and types, then generates the TypeScript SDK into `.modules/`.
  - ATS diagnostics such as `ASPIREATS001` are the build-time guardrail for export mistakes.
  - Capability IDs must be unique; when needed, use distinct export IDs plus `MethodName` to keep the generated TypeScript names clean.
- `aspire docs get multi-language-architecture`:
  - TypeScript AppHosts are guest processes that talk to the .NET host over local JSON-RPC.
  - We do not hand-write TypeScript bindings; the SDK is generated from the exported C# surface.
- `aspire docs get typescript-apphost-project-structure`:
  - A TypeScript AppHost needs `apphost.ts`, `aspire.config.json`, `package.json`, and `tsconfig.json`.
  - Local development can reference a hosting integration by `.csproj` path in `aspire.config.json`.
  - `.modules/` is generated and should not be edited directly.

## Repo Findings

- The Perl integration currently has no ATS annotations in `src/CommunityToolkit.Aspire.Hosting.Perl/**`.
- The current Perl sample logic lives in `examples/perl/cpanm-api-integration/CpanmApiIntegration.AppHost/AppHost.cs` and uses:
  - `AddPerlApi`
  - `WithCpanMinus`
  - `WithPackage`
  - `WithLocalLib`
  - `AddPerlScript`
  - `WithEnvironment`
  - `WithReference`
  - `WaitFor`
- Existing integrations with TypeScript support follow two patterns:
  - Export add-methods with `[AspireExport(...)]`.
  - Add ATS-friendly overloads or `[AspireExportIgnore]` when the public C# overload is not suitable for polyglot callers.
- Existing ATS-enabled integrations in this repo currently rely on the diagnostics that already come with the Aspire hosting toolchain; they do not add a separate analyzer package reference in the project file.
- Shared TypeScript validation already exists in `tests/CommunityToolkit.Aspire.Testing/TypeScriptAppHostTest.cs`, so Perl only needs a new test case instead of new harness code.

## Implementation Plan

1. Enable ATS in the Perl integration project.

    - Align the Perl project with the current repo ATS pattern rather than introducing a standalone analyzer package.
    - Suppress `ASPIREATS001` in the Perl project file, matching the pattern used by other ATS-exporting integrations.
    - Rely on the existing Aspire hosting toolchain to surface ATS diagnostics during build.

2. Export the Perl surface needed by a TypeScript AppHost.
   - Add `[AspireExport]` to the entry points in `src/CommunityToolkit.Aspire.Hosting.Perl/PerlAppResourceBuilderExtensions.cs`:
     - `AddPerlScript`
     - `AddPerlApi`
   - Keep `AddPerlModule` and `AddPerlExecutable` out of the TypeScript surface for now because they are not part of the first validated scenario and are not yet fully implemented in C#.
   - Export the fluent methods required by the first sample:
     - `WithCpanMinus`
     - `WithPackage`
     - `WithLocalLib`
   - Strong candidates for broader parity after the first pass:
     - `WithCarton`
     - `WithProjectDependencies`
     - `WithPerlbrew` / `WithPerlbrewEnvironment`
   - Expect to validate whether ATS accepts the current generic Perl fluent methods as-is.
     - If the analyzer rejects them or the generated surface is awkward, add concrete `PerlAppResource` polyglot overloads.
     - Use distinct export IDs and `MethodName` where needed.
     - Use `[AspireExportIgnore]` on overloads that should stay C#-only.

3. Add the first TypeScript AppHost example.
   - Create `examples/perl/cpanm-api-integration/CpanmApiIntegration.AppHost.TypeScript/`.
   - Include:
     - `apphost.ts`
     - `aspire.config.json`
     - `package.json`
     - `tsconfig.json`
     - `eslint.config.mjs`
     - `package-lock.json` if we keep parity with the committed TypeScript examples already in the repo
   - In `aspire.config.json`, reference the local integration project:
     - `CommunityToolkit.Aspire.Hosting.Perl`: `../../../../src/CommunityToolkit.Aspire.Hosting.Perl/CommunityToolkit.Aspire.Hosting.Perl.csproj`
   - Mirror the current C# sample behavior:
     - Perl API resource using `cpanm`, `Mojolicious::Lite`, `local::lib`, and an HTTP endpoint
     - Perl driver script with environment/reference/wait wiring
   - One implementation detail to verify after export generation:
     - Confirm the generated endpoint handle syntax for wiring the API URL into `withEnvironment` by inspecting `.modules/aspire.ts` after `aspire restore`.

4. Add TypeScript AppHost validation.
   - Add `tests/CommunityToolkit.Aspire.Hosting.Perl.Tests/TypeScriptAppHostTests.cs`.
   - Use the shared harness with:
     - `appHostProject: "CpanmApiIntegration.AppHost.TypeScript"`
     - `packageName: "CommunityToolkit.Aspire.Hosting.Perl"`
     - `exampleName: "perl/cpanm-api-integration"`
   - Initial validation settings should be:
     - `waitForResources: ["perl-api"]`
     - `waitStatus: "up"`
     - `requiredCommands: ["perl", "cpanm"]`
   - Rationale:
     - The sample API has an HTTP endpoint but no explicit health check, so `up` is safer than `healthy`.
     - The driver script may be short-lived, so it is not the best primary wait target for the first pass.
   - No test project file change should be needed unless we discover explicit compile includes later.

5. Validate the end-to-end flow.
   - Build the Perl integration project.
   - Run the Perl test project.
   - Run the new `TypeScriptAppHostTests`.
   - In the TypeScript example directory:
     - `npm ci`
     - `aspire restore`
     - inspect `.modules/aspire.ts`
     - `npx tsc --noEmit`
   - Confirm there are no ATS analyzer collisions and that the generated TypeScript names match the intended API shape.

## Recommended Cut Line

- Minimum viable TypeScript support for the first implementation pass:
  - ATS analyzer wiring
  - export the add-methods plus the three fluent methods used by the sample (`WithCpanMinus`, `WithPackage`, `WithLocalLib`)
  - create the TypeScript example
  - add the TypeScript AppHost test
- Defer until the first slice is green:
  - exporting the full Perl surface
  - `Perlbrew` support in TypeScript
  - certificate-trust/export edge cases
  - extra sample permutations beyond `cpanm-api-integration`

## Risks And Open Questions

- The main technical risk is ATS compatibility of the current generic Perl fluent methods.
- If `WithPackage` or `WithProjectDependencies` generate an awkward TypeScript signature, we may want dedicated polyglot overloads instead of exporting the raw C# shape.
- If the generated SDK does not make endpoint values directly consumable for `withEnvironment`, the sample may need a small adjustment from the current C# AppHost.
- Because analyzer versions are managed centrally in this repo, the package-version change will likely need to happen in `Directory.Packages.props`, not just the Perl project.

## First Implementation Order

1. Add analyzer/version plumbing.
2. Export `AddPerlApi`, `AddPerlScript`, `WithCpanMinus`, `WithPackage`, and `WithLocalLib`.
3. Generate the TypeScript example and inspect `.modules/aspire.ts`.
4. Adjust export shapes only if the analyzer or generated SDK forces it.
5. Add the TypeScript AppHost test and validate it.
