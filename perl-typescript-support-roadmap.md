# Perl TypeScript Support Migration Roadmap

This roadmap turns the existing support plan in [perl-typescript-support-plan.md](perl-typescript-support-plan.md) into reviewable execution chunks for adding TypeScript AppHost support to CommunityToolkit.Aspire.Hosting.Perl. The target started with the current `examples/perl/cpanm-api-integration` scenario first, and the finish line is now the full public Perl AppHost-facing C# surface: every public API should be exported to TypeScript, including experimental APIs, while still preferring TypeScript-friendly shapes where overload translation is needed. Based on the current plan and the existing ATS patterns already used elsewhere in the repo, there is no structural reason this migration cannot be made; the main risks are export-shape compatibility, generated SDK ergonomics, and environment-level validation.

Please as we do work on this roadmap's steps, keep the document up to date as we make changes.

## Roadmap Chart

| Step | Name | Small details | Blockers | Progress |
| --- | --- | --- | --- | --- |
| 1 | [Baseline and scope lock](#step-1-baseline-and-scope-lock) | Confirm the exact C# sample flow, the minimum Perl API surface for MVP, and the explicit non-goals for the first slice. | Hidden sample dependencies or fluent calls that are not yet listed in the support plan. | Complete |
| 2 | [Analyzer and project wiring](#step-2-analyzer-and-project-wiring) | Align the Perl project with the ATS warning and suppression pattern already used by ATS-enabled integrations in the repo. | Repo-specific analyzer behavior may differ from older docs that mention a standalone package. | Complete |
| 3 | [Export surface design](#step-3-export-surface-design) | Decide which APIs export cleanly as-is and which need polyglot overloads, ignores, or renamed methods. | Generic, callback-based, or parameter-ordering-hostile signatures that produce awkward TypeScript. | Complete |
| 4 | [Export add-method entry points](#step-4-export-add-method-entry-points) | Export the top-level Perl resource creation methods needed for the first scenario. | Analyzer rejects a signature or a resource type is not exportable without reshaping. | Complete |
| 5 | [Export fluent methods for the MVP chain](#step-5-export-fluent-methods-for-the-mvp-chain) | Export the fluent methods the sample actually needs, with ATS-specific overloads where needed. | Generic chain methods may need dedicated `PerlAppResource` overloads to stay natural in TypeScript. | Complete |
| 6 | [Generate and inspect the TypeScript SDK](#step-6-generate-and-inspect-the-typescript-sdk) | Restore and inspect `.modules/aspire.ts` before building the TypeScript example around it. | Generated method names, return types, or endpoint handles may not match the intended call style. | Complete |
| 7 | [Scaffold the TypeScript AppHost example](#step-7-scaffold-the-typescript-apphost-example) | Create the TypeScript AppHost project structure and wire it to the local Perl integration project. | Missing config details, package-lock policy decisions, or example-folder conventions. | Complete |
| 8 | [Port the sample behavior idiomatically](#step-8-port-the-sample-behavior-idiomatically) | Recreate the existing sample flow in `apphost.ts` using generated exports, not hand-written wrappers. | Endpoint/reference wiring may expose a rough API shape that needs a return to step 3 or 5. | Complete |
| 9 | [Add automated TypeScript AppHost validation](#step-9-add-automated-typescript-apphost-validation) | Add the Perl-specific test case that uses the shared TypeScript AppHost validation harness. | Wait targets, resource status selection, or command prerequisites may need tuning. | Complete |
| 10 | [Run end-to-end validation and capture follow-up work](#step-10-run-end-to-end-validation-and-capture-follow-up-work) | Execute the narrow and full validation passes, then record any deferred parity work. | Windows Perl environment issues, ATS edge cases, or sample-specific runtime behavior. | Complete |
| 11 | [Lock the full-capability finish line](#step-11-lock-the-full-capability-finish-line) | Define exactly what counts toward 100% TypeScript capability coverage and turn the remaining public Perl APIs into a tracked matrix. | Remaining gaps now come from unexported or unvalidated public APIs, not from policy ambiguity about experimental surface area. | Complete |
| 12 | [Export package-manager parity methods](#step-12-export-package-manager-parity-methods) | Export `WithCarton` and `WithProjectDependencies` and confirm their generated TypeScript shapes are acceptable. | `cartonDeployment` option shape or Carton/WithPackage interaction may need polyglot-specific handling. | Complete |
| 13 | [Validate project-dependency scenarios](#step-13-validate-project-dependency-scenarios) | Add TypeScript AppHost examples and tests for `cpanm --installdeps` and Carton-based dependency flows. | The scenarios now depend on normal environment prerequisites (`perl`, `cpanm`, `carton`, Aspire restore) rather than missing fixture work. | Complete |
| 14 | [Design and export the Perlbrew surface](#step-14-design-and-export-the-perlbrew-surface) | Export both public Perlbrew helpers and validate their Linux/Windows behavior in TypeScript. | Linux-only runtime behavior and Windows-specific failure semantics still need an explicit validation story. | Not started |
| 15 | [Validate Perlbrew across platforms](#step-15-validate-perlbrew-across-platforms) | Add Linux-positive TypeScript validation and explicit Windows behavior checks for the Perlbrew flow. | Perlbrew is Linux-only, so the validation matrix likely needs platform-aware test gating or a Linux-only lane. | Not started |
| 16 | [Export the certificate-trust capability](#step-16-export-the-certificate-trust-capability) | Export `WithPerlCertificateTrust` as part of the public parity target and validate its TypeScript behavior. | End-to-end validation may require more infrastructure than the export itself. | Not started |
| 17 | [Validate certificate-trust behavior](#step-17-validate-certificate-trust-behavior) | Add a TypeScript scenario that proves certificate bundle propagation to both runtime and installer paths. | A realistic HTTPS fixture and cross-platform certificate handling may be harder than the export itself. | Not started |
| 18 | [Finish module and executable support in C#](#step-18-finish-module-and-executable-support-in-c) | Close the underlying C# behavior gaps for `AddPerlModule` and `AddPerlExecutable` before exporting them. | The methods exist today, but runtime, publish, or sample-level behavior may still be under-specified for full support. | Not started |
| 19 | [Export and validate module and executable entry points](#step-19-export-and-validate-module-and-executable-entry-points) | Export `AddPerlModule` and `AddPerlExecutable` only after the C# implementation is promoted to supported behavior. | The generated SDK can be added quickly, but example and runtime proof may lag behind implementation work. | Not started |
| 20 | [Close the parity gap](#step-20-close-the-parity-gap) | Run the final parity audit, add any missing test lanes, and update docs so the supported TypeScript surface is unambiguous. | CI coverage or platform-specific prerequisites may still leave one capability unverified unless the matrix is widened. | Not started |

## Step 1: Baseline and scope lock

Goal

1. Confirm the exact behavior of the current C# sample and turn that into an agreed MVP contract for the TypeScript migration.
2. Make the cut line explicit so review can happen before any ATS annotations are added.

Do in this chunk

1. Read the existing C# AppHost sample and enumerate every Perl-specific call in order.
2. Confirm the first-pass exported surface includes `AddPerlApi`, `AddPerlScript`, `WithCpanMinus`, `WithPackage`, and `WithLocalLib`, plus the existing shared Aspire wiring methods already available to polyglot app hosts.
3. Record the first-pass non-goals: full Perl surface parity, Perlbrew support, Carton support, project dependency helpers, and extra example permutations.
4. Confirm what the TypeScript sample must prove: package installation, API startup, environment wiring, reference wiring, and a wait strategy that is stable for automation.

Stop and review when

1. There is a written MVP list that a reviewer can sign off on before code changes start.
2. Any method not needed for the first example is explicitly marked as later work instead of silently sliding into scope.

Exit criteria

1. The migration target is a single supported scenario with a fixed method inventory.
2. The first implementation cut line is small enough to review comfortably.

Likely blockers

1. The current sample may rely on behavior that is only obvious after reading helper methods or nearby resource code.
2. The first scenario may need one extra exported method that is not yet listed in the support plan.

Completion notes

1. The first validated scenario is locked to `examples/perl/cpanm-api-integration/CpanmApiIntegration.AppHost/AppHost.cs` with no extra Perl-specific calls beyond the ones already identified in the support plan.
2. The Perl-specific call order in the current sample is fixed as: `AddPerlApi`, `WithCpanMinus`, `WithPackage`, `WithLocalLib`, and `AddPerlScript`.
3. The current sample also depends on existing shared Aspire builder methods that should already remain outside the Perl-specific MVP export set: `WithHttpEndpoint`, `WithEnvironment`, `WithReference`, `WaitFor`, and `GetEndpoint`.
4. The MVP Perl-specific export inventory is locked to `AddPerlApi`, `AddPerlScript`, `WithCpanMinus`, `WithPackage`, and `WithLocalLib` for the first TypeScript scenario.
5. The first TypeScript example must prove package installation, Perl API startup, endpoint-to-environment wiring, resource reference wiring, and stable wait behavior for automation.
6. Deferred non-goals remain full Perl surface parity, `AddPerlModule`, `AddPerlExecutable`, Perlbrew helpers, Carton helpers, project dependency helpers, and extra sample permutations beyond `cpanm-api-integration`.
7. No additional Perl-specific blocker was found during the sample read; the remaining uncertainty is export ergonomics rather than hidden sample scope.

[Back to roadmap chart](#roadmap-chart)

## Step 2: Analyzer and project wiring

Goal

1. Turn on the ATS build-time guardrails in the same way the repo uses them for other polyglot-ready integrations.

Do in this chunk

1. Verify how existing ATS-enabled integrations in this repo actually get ATS diagnostics.
2. Apply the same `ASPIREATS001` suppression pattern already used by existing ATS-enabled integrations in this repo.
3. Avoid adding a standalone analyzer package unless the current repo build actually requires it.
4. Build the Perl project to confirm the ATS warnings still flow through the current Aspire hosting toolchain and the project still restores cleanly.

Stop and review when

1. The diff is only package-management and project-wiring work.
2. Restore and build behavior are stable before any public API annotations are touched.

Exit criteria

1. The Perl integration participates in ATS validation during normal build flows.
2. The Perl project matches the repo's existing ATS project configuration pattern.

Likely blockers

1. Older ATS documentation may mention a standalone package that is not used in this repo's current package and feed setup.
2. The Perl project may still need a small repo-specific adjustment if the current build path differs from other ATS-enabled integrations.

Completion notes

1. The Perl project now matches the current repo pattern by suppressing `ASPIREATS001` in the project file, consistent with other ATS-enabled integrations.
2. Existing ATS-enabled integrations in this repo do not add a standalone analyzer package reference; the current Aspire hosting toolchain already supplies the relevant ATS diagnostics.
3. Attempting to add standalone analyzer packages failed against the repo's configured feeds, so that path was rejected and removed rather than forcing broader NuGet source changes.
4. The final step 2 configuration kept the repo's existing feed mapping unchanged and aligned the Perl project to local precedent instead.
5. Perl-only validation passed after the final step 2 configuration, so the migration can proceed to export-surface design from a clean baseline.

[Back to roadmap chart](#roadmap-chart)

## Step 3: Export surface design

Goal

1. Decide the exact exported API shape before implementation so the TypeScript surface feels natural and stable.
2. Make the idiomatic-language rule explicit: keep TypeScript ergonomic even if that means not exposing every C# overload directly.

Do in this chunk

1. Review the Perl extension methods that are candidates for export.
2. Classify each method into one of four buckets: export as-is, export with a renamed method, export through a polyglot-specific overload, or ignore for polyglot callers.
3. Apply the same design logic used elsewhere in the repo for polyglot-friendly APIs: avoid callback-based signatures, avoid awkward optional parameter ordering, and avoid exposing types that do not translate well through ATS.
4. Decide where distinct export IDs and `MethodName` values are needed to keep generated TypeScript names clean.
5. Record any methods that should remain C#-only during the first pass.

Stop and review when

1. There is a simple export matrix that a reviewer can inspect before the implementation starts.
2. The team agrees that the TypeScript sample should use generated, idiomatic calls rather than force-fitting awkward C# signatures.

Exit criteria

1. Every MVP method has an agreed export strategy.
2. The plan is explicit about where `[AspireExportIgnore]` or internal exported overloads may be required.

Likely blockers

1. Generic fluent methods may technically export but still produce poor TypeScript ergonomics.
2. Endpoint-related values may need shape adjustments once the generated SDK is inspected.

Completion notes

1. The first-pass add-method surface is simple and ATS-friendly as written: `AddPerlApi` and `AddPerlScript` both take only the builder, a resource name, and string path values, so they do not need polyglot-only overloads for the first export pass.
2. No distinct export IDs or `MethodName` overrides are needed for the first-pass add methods because `addPerlApi` and `addPerlScript` already map cleanly to idiomatic TypeScript names.
3. `AddPerlModule` and `AddPerlExecutable` are also string-based, but they remain intentionally deferred because they are outside the validated `cpanm-api-integration` scenario and are not yet fully implemented on the C# side.
4. The MVP fluent methods under consideration remain `WithCpanMinus`, `WithPackage`, and `WithLocalLib`; each currently has scalar arguments only, so the current design assumption is that they can be tried as-is in step 5 before introducing polyglot-specific overloads.
5. `WithCarton` and `WithProjectDependencies` are deferred for first-pass scope reasons rather than ATS-shape reasons.
6. `WithPerlbrew` and `WithPerlbrewEnvironment` are deferred for the first pass because they add a second overlapping concept to the TypeScript surface and are not needed for the first validated scenario.

Export matrix

| Method | First-pass decision | Export strategy | Reason |
| --- | --- | --- | --- |
| `AddPerlApi` | Include now | Export as-is | String-only ATS-friendly signature; needed by the sample. |
| `AddPerlScript` | Include now | Export as-is | String-only ATS-friendly signature; needed by the sample. |
| `AddPerlModule` | Defer | None in first pass | Not needed for the MVP sample and not fully implemented in C# yet. |
| `AddPerlExecutable` | Defer | None in first pass | Not needed for the MVP sample and not fully implemented in C# yet. |
| `WithCpanMinus` | Include later in MVP | Try export as-is in step 5 | No callback or complex parameter shape. |
| `WithPackage` | Include later in MVP | Try export as-is in step 5 | Scalar parameters only; ergonomics to confirm after generation. |
| `WithLocalLib` | Include later in MVP | Try export as-is in step 5 | Scalar parameter only; ergonomics to confirm after generation. |
| `WithCarton` | Defer | None in first pass | Broader package-manager surface than the MVP needs. |
| `WithProjectDependencies` | Defer | None in first pass | Broader package-manager surface than the MVP needs. |
| `WithPerlbrew` | Defer | None in first pass | Not needed for MVP and overlaps conceptually with `WithPerlbrewEnvironment`. |
| `WithPerlbrewEnvironment` | Defer | None in first pass | Not needed for MVP and better revisited after the basic TypeScript surface is proven. |

[Back to roadmap chart](#roadmap-chart)

## Step 4: Export add-method entry points

Goal

1. Make the Perl resource creation entry points available to a TypeScript AppHost.

Do in this chunk

1. Add `[AspireExport]` to the agreed Perl add-methods needed for the first implementation pass.
2. Preserve the existing public C# API shape unless a polyglot-specific overload is required.
3. If the analyzer rejects a public signature, add a focused polyglot overload instead of widening the scope of the change.
4. Keep naming aligned with the generated TypeScript method style expected in the sample.

Stop and review when

1. Only the top-level add methods are in the diff.
2. Reviewers can confirm naming and surface area before fluent methods are added.

Exit criteria

1. The add-method exports compile with the analyzer enabled.
2. The exported entry points cover the first sample's resource creation needs.

Likely blockers

1. One of the resource creation methods may depend on a parameter or type that is not ATS-friendly.
2. Method naming may need refinement once the generated TypeScript output is visible.

Completion notes

1. `AddPerlScript` now exports directly as `addPerlScript` without a polyglot-specific overload.
2. `AddPerlApi` now exports directly as `addPerlApi` without a polyglot-specific overload.
3. The exported add-method entry points preserved the existing public C# signatures; no parameter reshaping or internal ATS-only overloads were needed at this stage.
4. `AddPerlModule` and `AddPerlExecutable` were intentionally left untouched in this step to preserve the first-pass scope agreed in step 3 and because they are not yet fully implemented in C#.
5. Perl-only validation passed after the add-method exports were added, so the next migration slice can move to the fluent-method exports in step 5.

[Back to roadmap chart](#roadmap-chart)

## Step 5: Export fluent methods for the MVP chain

Goal

1. Export the chainable Perl methods needed to express the first sample cleanly in TypeScript.

Do in this chunk

1. Add exports for `WithCpanMinus`, `WithPackage`, and `WithLocalLib`.
2. Validate whether the current generic signatures are good enough for TypeScript callers.
3. If they are not, add ATS-focused overloads on `IResourceBuilder<PerlAppResource>` or another concrete resource shape so the generated API remains readable.
4. Mark raw C#-only overloads with `[AspireExportIgnore]` where the polyglot call story would otherwise be confusing.
5. Defer broader fluent parity until the first chain is working end to end.

Stop and review when

1. The Perl fluent chain used by the sample can be represented without obvious TypeScript friction.
2. Any extra parity work is clearly documented as deferred rather than folded into the MVP.

Exit criteria

1. The minimum fluent chain for the sample exists in an exportable form.
2. The code makes an intentional trade-off between C# API fidelity and TypeScript ergonomics where needed.

Likely blockers

1. A generic fluent helper may compile but generate an awkward or unstable TypeScript signature.
2. The best polyglot overload may need a separate method name to avoid export collisions.

Completion notes

1. `WithCpanMinus`, `WithPackage`, and `WithLocalLib` now export directly from the existing generic methods; no ATS-specific overloads were required for the first pass.
2. The first-pass fluent export set stayed intentionally narrow and did not widen into `WithCarton`, `WithProjectDependencies`, `WithPerlbrew`, or `WithPerlbrewEnvironment`.
3. Perl-only validation passed after the fluent exports were added, so the next check moved to generated SDK shape rather than additional compile-only changes.
4. The remaining question for these methods became purely ergonomic: whether the generated TypeScript signatures read naturally enough for the first sample chain.

[Back to roadmap chart](#roadmap-chart)

## Step 6: Generate and inspect the TypeScript SDK

Goal

1. Inspect the real generated TypeScript surface before locking in the sample implementation.

Do in this chunk

1. Run the generation flow that produces `.modules/aspire.ts` for the new exports.
2. Inspect method names, chaining behavior, parameter order, optional values, and endpoint/reference-related shapes.
3. Confirm the generated API reads like TypeScript and not like a mechanically translated C# API.
4. If the generated surface is awkward, return to step 3 or 5 and fix the exports before building more code on top of them.

Stop and review when

1. There is a concrete generated SDK surface to review.
2. The team can decide whether the API shape is good enough before the example is written.

Exit criteria

1. The generated SDK supports the desired sample call style.
2. Any remaining shape issues are corrected before the TypeScript example becomes the reference implementation.

Likely blockers

1. `withEnvironment` and endpoint handle wiring may need one more export-shape adjustment.
2. Export IDs or method names may collide or read poorly once generation happens.

Completion notes

1. A temporary scratch TypeScript AppHost was used to run `aspire restore` against the local Perl project without pulling step 7 into scope early.
2. The generated builder surface kept the exported add methods simple and positional: `addPerlApi(resourceName, appDirectory, scriptName)` and `addPerlScript(resourceName, appDirectory, scriptName)`.
3. The generated fluent surface is acceptable for the MVP sample:
   - `withCpanMinus()` stays parameterless.
   - `withPackage(packageName, { force, skipTest })` becomes a natural TypeScript call shape.
   - `withLocalLib({ path })` becomes an options object with an optional `path` property.
4. The generated `PerlAppResource` surface still includes `withEnvironment`, `withReference`, `waitFor`, and `getEndpoint`, so the planned sample wiring remains viable.
5. The generated `withEnvironment` signature accepts `EndpointReference` and `Awaitable<EndpointReference>`, which means the planned `API_URL` wiring can use the Perl API endpoint directly.
6. No export-shape correction was needed after SDK inspection, so the migration can proceed to the real TypeScript AppHost scaffold in step 7.

[Back to roadmap chart](#roadmap-chart)

## Step 7: Scaffold the TypeScript AppHost example

Goal

1. Create the TypeScript AppHost project structure for the Perl sample with the minimum files needed for restore, lint, and type-checking.

Do in this chunk

1. Create `examples/perl/cpanm-api-integration/CpanmApiIntegration.AppHost.TypeScript/`.
2. Add `apphost.ts`, `aspire.config.json`, `package.json`, `tsconfig.json`, and `eslint.config.mjs`.
3. Decide whether `package-lock.json` should be committed for parity with other TypeScript examples in the repo.
4. Point `aspire.config.json` at the local Perl integration project so the generated module comes from the current working tree.
5. Keep the scaffold intentionally small so reviewers can focus on structure before behavior.

Stop and review when

1. The example project exists and restores cleanly.
2. The folder layout and config choices match repo conventions well enough to avoid churn later.

Exit criteria

1. The TypeScript example can run `npm ci`, `aspire restore`, and `npx tsc --noEmit` with placeholder or near-placeholder app code.
2. The example points at the local Perl integration project rather than a published package.

Likely blockers

1. The repo may have an unwritten convention around lock files or ESLint config shape that needs to be copied.
2. TypeScript project structure may need a small adjustment after the first restore.

Completion notes

1. The new example folder `examples/perl/cpanm-api-integration/CpanmApiIntegration.AppHost.TypeScript/` now exists with `apphost.ts`, `aspire.config.json`, `package.json`, `tsconfig.json`, and `eslint.config.mjs`.
2. `aspire.config.json` points at the local Perl hosting project with the same relative `packages` mapping pattern used by the shipped TypeScript examples in this repo.
3. `npm install` was used to generate a committed `package-lock.json`, matching the repo's existing TypeScript example pattern.
4. The scaffold validated successfully with `npm install`, `aspire restore`, and `npm run build` before any Perl-specific behavior was added.
5. The current `apphost.ts` remains intentionally minimal so the next step can focus review on the Perl sample behavior itself rather than structure and config churn.

[Back to roadmap chart](#roadmap-chart)

## Step 8: Port the sample behavior idiomatically

Goal

1. Recreate the existing Perl sample behavior in `apphost.ts` using the generated API in a way that feels natural to a TypeScript AppHost author.

Do in this chunk

1. Mirror the current sample's Perl API resource creation and package-install setup.
2. Add the driver script resource and the needed environment, reference, and wait wiring.
3. Keep the TypeScript code idiomatic: prefer the generated TypeScript names, avoid workarounds that leak raw C# implementation details into the example, and treat awkward generated calls as an export-design issue instead of a sample-authoring issue.
4. Verify how endpoint values should be passed into `withEnvironment` after generation.
5. Keep behavioral parity with the current C# sample while allowing TypeScript syntax and naming to look native.

Stop and review when

1. The example expresses the same scenario as the C# sample without obvious API awkwardness.
2. Any place where the example starts to look like a workaround is captured as an export-shape bug to fix.

Exit criteria

1. The TypeScript example is readable as a first-class AppHost sample.
2. The example proves the exported surface is good enough for real use, not just technically callable.

Likely blockers

1. Endpoint/reference values may not plug into the fluent chain exactly as expected on the first attempt.
2. A hidden assumption in the current C# sample may require one more exported helper.

Completion notes

1. The TypeScript AppHost now mirrors the current C# sample behavior with a Perl API resource and a Perl driver resource.
2. The Perl API uses the same first-pass behavior as the C# sample: `addPerlApi`, `withCpanMinus`, `withPackage("Mojolicious::Lite", { force: true, skipTest: true })`, `withLocalLib({ path: "local" })`, and `withHttpEndpoint({ name: "http", env: "PORT" })`.
3. The Perl driver uses `addPerlScript`, `withEnvironment("API_URL", perlApi.getEndpoint("http"))`, `withReference(perlApi)`, and `waitFor(perlApi)`.
4. The only correction needed during validation was the local project path in `aspire.config.json`; once fixed, `aspire restore` and `npm run build` both passed.
5. The generated TypeScript surface remained idiomatic enough that no additional ATS-specific overloads were required for this sample port.

[Back to roadmap chart](#roadmap-chart)

## Step 9: Add automated TypeScript AppHost validation

Goal

1. Add the Perl-specific automated validation that exercises the new TypeScript AppHost example through the shared test harness.

Do in this chunk

1. Add [tests/CommunityToolkit.Aspire.Hosting.Perl.Tests/TypeScriptAppHostTests.cs](tests/CommunityToolkit.Aspire.Hosting.Perl.Tests/TypeScriptAppHostTests.cs).
2. Configure the test to use `CpanmApiIntegration.AppHost.TypeScript`, `CommunityToolkit.Aspire.Hosting.Perl`, and `perl/cpanm-api-integration`.
3. Start with the expected wait target and status from the support plan, then adjust only if real execution proves they are wrong.
4. Include any required command checks such as `perl` and `cpanm`.
5. Keep this step focused on wiring the shared harness, not inventing a new validation path.

Stop and review when

1. The test diff is isolated and easy to evaluate.
2. The validation contract for the example is explicit enough for later maintenance.

Exit criteria

1. The new test compiles and exercises the shared TypeScript AppHost validation flow.
2. The expected wait resource and status are based on real sample behavior.

Likely blockers

1. The API resource may need a different wait target or status than the initial guess.
2. Short-lived helper resources may make the first validation setup too optimistic.

Completion notes

1. Added [tests/CommunityToolkit.Aspire.Hosting.Perl.Tests/TypeScriptAppHostTests.cs](tests/CommunityToolkit.Aspire.Hosting.Perl.Tests/TypeScriptAppHostTests.cs) and wired it to `CpanmApiIntegration.AppHost.TypeScript`, `CommunityToolkit.Aspire.Hosting.Perl`, and `perl/cpanm-api-integration`.
2. The validated wait contract is `waitForResources: ["perl-api"]` with `waitStatus: "up"`, plus required command checks for `perl` and `cpanm`.
3. The shared validation harness now supports a `useConfiguredPackages` mode so examples can be validated against the local package mapping already present in `aspire.config.json` when that is the intended development shape.
4. Validation exposed that the Perl TypeScript AppHost was missing the same `profiles` startup metadata used by the other working TypeScript AppHost examples; adding the `https` profile with dashboard and resource-service endpoint settings fixed the detached `aspire start` failure.
5. After those fixes, the focused Perl TypeScript AppHost test passed end to end through restore, TypeScript compilation, `aspire start`, resource wait, and `aspire describe`.

[Back to roadmap chart](#roadmap-chart)

## Step 10: Run end-to-end validation and capture follow-up work

Goal

1. Prove the full path works and document what remains after the MVP is green.

Do in this chunk

1. Run the narrow build and test checks for the touched code.
2. Run the Perl test project and the new TypeScript AppHost test.
3. In the example folder, run `npm ci`, `aspire restore`, inspect `.modules/aspire.ts`, and run `npx tsc --noEmit`.
4. Record the actual results in this roadmap, including any export-shape adjustments or environment-specific issues discovered during validation.
5. Capture the deferred follow-up list: full fluent parity, Perlbrew support, Carton, project dependency helpers, and any Windows-specific environment caveats.

Stop and review when

1. There is a complete validation record for the MVP slice.
2. Follow-up work is separated cleanly from the first successful migration.

Exit criteria

1. The first TypeScript AppHost Perl scenario is green end to end, or the remaining blockers are concrete and narrowly scoped.
2. The roadmap reflects what was actually learned, not just the original guess.

Likely blockers

1. Environment-specific behavior on Windows may require a documented caveat even if the export work is correct.
2. One remaining ATS issue may only become visible after the full example and test flow run together.

Completion notes

1. End-to-end example validation passed in the TypeScript AppHost folder with `npm ci`, `aspire restore --apphost apphost.ts --non-interactive`, and `npx tsc --noEmit`.
2. The generated SDK in `.modules/aspire.ts` now confirms the intended Perl-specific surface exactly: `addPerlApi`, `addPerlScript`, `withCpanMinus`, `withPackage`, and `withLocalLib` are present; `addPerlModule` and `addPerlExecutable` are intentionally absent.
3. The missing `addPerlModule` and `addPerlExecutable` methods are not a regression in ATS export wiring; they remain deferred because those entry points are not fully implemented on the C# side and are outside the first validated scenario.
4. The focused TypeScript AppHost validation test passed, and the full Perl test project also passed with 148 of 148 tests green.
5. The remaining deferred follow-up list is broader parity work rather than MVP breakage: Carton helpers, project dependency helpers, Perlbrew-related surface, and any later revisit of module/executable support after the underlying C# implementation is finished.

[Back to roadmap chart](#roadmap-chart)

## Step 11: Lock the full-capability finish line

Goal

1. Define what 100% TypeScript capability coverage means for the Perl integration so the remaining work can be measured cleanly.
2. Turn the remaining public Perl AppHost APIs into a tracked matrix with an explicit status for each capability.

Do in this chunk

1. Inventory every remaining public Perl AppHost-facing API that is not yet available from the generated TypeScript SDK.
2. Split the inventory into three buckets: ready to export now, blocked on a C# implementation gap, and experimental or policy-dependent.
3. Decide whether alias-style APIs such as `WithPerlbrew` should be exported directly, ignored, or collapsed into a single canonical TypeScript method.
4. Decide whether experimental APIs such as `WithPerlCertificateTrust` count toward the 100% parity finish line now or only after an explicit support decision.
5. Record the validation expectation for each capability: generated SDK presence, compile-time usage, runtime scenario, and platform-specific negative behavior where relevant.

Stop and review when

1. There is a single finish-line matrix that says exactly what work remains.
2. Reviewers can see which gaps are export-only versus blocked on deeper C# support.

Exit criteria

1. The roadmap has a precise remaining capability list instead of a generic parity aspiration.
2. Every later step can point back to a specific capability bucket in this matrix.

Likely blockers

1. The team may need to decide whether experimental and partially implemented APIs count toward “100%” immediately.
2. One alias or convenience method may be intentionally C#-only even if the underlying behavior is supported.

Completion notes

1. The parity target is now explicit: every public Perl AppHost-facing C# API counts toward the TypeScript finish line, including experimental APIs.
2. Alias-style public APIs stay literal to the C# surface for parity purposes, so `WithPerlbrew` and `WithPerlbrewEnvironment` both remain in-scope rather than being collapsed into one canonical TypeScript method.
3. `WithPerlCertificateTrust` counts toward the finish line immediately even though it remains experimental on the C# side.
4. The matrix below is now the source of truth for what is exported, what is validated, and what remains.

Finish-line matrix

| API | Public parity status | TypeScript status | Validation expectation | Remaining work |
| --- | --- | --- | --- | --- |
| `AddPerlApi` | Counts now | Exported | Generated SDK presence, compile-time usage, runtime-backed AppHost scenario | None for current parity slice |
| `AddPerlScript` | Counts now | Exported | Generated SDK presence, compile-time usage, runtime-backed AppHost scenario | None for current parity slice |
| `AddPerlModule` | Counts now | Not yet exported | Generated SDK presence, compile-time usage, runtime-backed AppHost scenario | Export and add dedicated TypeScript validation |
| `AddPerlExecutable` | Counts now | Not yet exported | Generated SDK presence, compile-time usage, runtime-backed AppHost scenario | Export and add dedicated TypeScript validation |
| `WithCpanMinus` | Counts now | Exported | Generated SDK presence, compile-time usage, runtime-backed AppHost scenario | None for current parity slice |
| `WithPackage` | Counts now | Exported | Generated SDK presence, compile-time usage, runtime-backed AppHost scenario | None for current parity slice |
| `WithLocalLib` | Counts now | Exported | Generated SDK presence, compile-time usage, runtime-backed AppHost scenario | None for current parity slice |
| `WithCarton` | Counts now | Exported | Generated SDK presence, compile-time usage, runtime-backed AppHost scenario | None for current parity slice |
| `WithProjectDependencies` | Counts now | Exported | Generated SDK presence, compile-time usage, runtime-backed AppHost scenario, deployment-lockfile path | None for current parity slice |
| `WithPerlbrew` | Counts now | Not yet exported | Generated SDK presence, compile-time usage, Linux-positive runtime behavior, Windows-negative behavior | Export and validate |
| `WithPerlbrewEnvironment` | Counts now | Not yet exported | Generated SDK presence, compile-time usage, Linux-positive runtime behavior, Windows-negative behavior | Export and validate |
| `WithPerlCertificateTrust` | Counts now, experimental | Not yet exported | Generated SDK presence, compile-time usage, runtime env propagation, installer env propagation | Export and validate |

[Back to roadmap chart](#roadmap-chart)

## Step 12: Export package-manager parity methods

Goal

1. Expose the remaining package-manager capabilities needed for TypeScript parity with the supported Perl package flows.

Do in this chunk

1. Add TypeScript exports for `WithCarton` and `WithProjectDependencies`.
2. Inspect the generated SDK shape for `cartonDeployment` and confirm it reads naturally for TypeScript callers.
3. Keep the C# interaction rules intact, especially the existing `WithPackage()` and `WithCarton()` incompatibility.
4. Add ATS-specific overloads or ignore wrappers only if the generated TypeScript API is awkward or misleading.

Stop and review when

1. The generated TypeScript package-manager surface is readable without explaining C# implementation details.
2. Negative interaction rules remain explicit rather than hidden in runtime exceptions alone.

Exit criteria

1. `WithCarton` and `WithProjectDependencies` appear in `.modules/aspire.ts` with acceptable TypeScript call shapes.
2. The export diff is limited to package-manager parity and does not widen into unrelated capability work.

Likely blockers

1. `WithProjectDependencies(cartonDeployment: true)` may generate an options shape that needs a TypeScript-specific overload.
2. The Carton and per-package installer interaction may need clearer guidance in generated or human-facing docs.

Completion notes

1. Added `AspireExport` annotations to `WithCarton` and `WithProjectDependencies` without changing their existing C# interaction rules.
2. The TypeScript scenario now exercises the intended generated call shape for deployment mode as `withProjectDependencies({ cartonDeployment: true })`.
3. The existing `WithPackage()` and `WithCarton()` incompatibility remains unchanged and continues to be enforced by the underlying C# implementation.
4. Focused package-manager regression coverage passed after the export change: `WithCartonTests` and `WithProjectDependenciesTests` ran green.

[Back to roadmap chart](#roadmap-chart)

## Step 13: Validate project-dependency scenarios

Goal

1. Prove the exported package-manager parity surface works in real TypeScript AppHost scenarios, not just in generated type signatures.

Do in this chunk

1. Add a TypeScript AppHost scenario for `WithProjectDependencies()` in the cpanm flow.
2. Add a Carton-focused TypeScript AppHost scenario that exercises `.WithCarton().WithProjectDependencies()` with the right `cpanfile` expectations.
3. Add shared-harness tests for both scenarios and keep the wait strategy stable for automation.
4. Cover the `cpanfile.snapshot` requirement and any command prerequisites explicitly in the tests.

Stop and review when

1. There is at least one runtime-backed TypeScript AppHost test for each supported project-dependency path.
2. Reviewers can see the difference between type-only coverage and behavior coverage.

Exit criteria

1. The TypeScript AppHost test suite proves both cpanm-based and Carton-based project dependency flows.
2. Any missing runtime prerequisite is captured as a concrete blocker rather than a vague parity gap.

Likely blockers

1. The Carton scenario may need new example fixtures such as `cpanfile` and `cpanfile.snapshot`.
2. Command availability such as `carton` may require new required-command gating in the test matrix.

Completion notes

1. The TypeScript AppHost example now focuses on a single Carton-backed Perl API resource plus the existing driver resource, instead of registering multiple nearly identical API resources in one AppHost.
2. The C# AppHost in the same example continues to cover the `cpanm` package-install flow, while the TypeScript AppHost now exercises a different public surface area with `withCarton().withProjectDependencies({ cartonDeployment: true })`.
3. The TypeScript AppHost now reuses the shared `examples/perl/cpanm-api-integration/scripts/API.pl` entrypoint; the Carton `cpanfile` and `cpanfile.snapshot` live beside that shared script so `withProjectDependencies()` installs from the real working directory rather than a duplicate fixture app.
4. The shared-harness Perl TypeScript AppHost test now waits for both `perl-api` and `perl-driver`, and it requires `perl` plus `carton` for the TypeScript scenario.
5. Focused runtime validation passed for the simplified Carton scenario through the shared TypeScript AppHost harness after correcting a local user-level NuGet source issue in the validation environment.
6. The full Perl test project also passed after these changes with 174 of 174 tests green.

[Back to roadmap chart](#roadmap-chart)

## Step 14: Design and export the Perlbrew surface

Goal

1. Export the two public Perlbrew methods into TypeScript and validate them with intentional platform-aware coverage.

Do in this chunk

1. Export both `WithPerlbrew` and `WithPerlbrewEnvironment` because both public C# methods now count toward the parity target.
2. Reshape arguments only if that materially improves TypeScript readability without dropping either public method.
3. Preserve the current Windows failure behavior and Linux-only support constraints.
4. Confirm the generated method names make the Perlbrew flow understandable without leaking internal C# naming history.

Stop and review when

1. The Perlbrew TypeScript API does not present duplicate or confusing entry points.
2. The support decision is explicit enough that later docs and tests can follow it consistently.

Exit criteria

1. The Perlbrew capability appears in the generated SDK with a deliberate TypeScript shape.
2. The roadmap records whether alias-style C# methods remain C#-only or are intentionally exported.

Likely blockers

1. The two public Perlbrew methods overlap semantically, so the TypeScript story still needs careful naming and documentation even though both methods remain in scope.
2. Windows-specific failure messaging may complicate what “supported in TypeScript” means for this capability.

[Back to roadmap chart](#roadmap-chart)

## Step 15: Validate Perlbrew across platforms

Goal

1. Prove the Perlbrew TypeScript story works where supported and fails predictably where unsupported.

Do in this chunk

1. Add a Linux-positive TypeScript AppHost scenario that exercises the Perlbrew flow end to end.
2. Add explicit coverage for the current Windows path so the expected failure behavior is documented and tested.
3. Extend the shared validation harness or test conventions if platform gating is required.
4. Record any Linux-only prerequisites or CI-lane needs rather than assuming the existing Windows flow is enough.

Stop and review when

1. Platform behavior is explicit and repeatable.
2. Reviewers can see what is genuinely supported on Linux and what is intentionally unsupported on Windows.

Exit criteria

1. The Perlbrew TypeScript capability has both positive and negative validation where appropriate.
2. Platform-specific requirements are captured in tests or docs rather than tribal knowledge.

Likely blockers

1. Perlbrew is Linux-only, so an additional test lane or devcontainer validation path may be required.
2. The existing shared harness may need a small extension to express OS-specific expectations cleanly.

[Back to roadmap chart](#roadmap-chart)

## Step 16: Export the certificate-trust capability

Goal

1. Export `WithPerlCertificateTrust` and validate it as part of the public TypeScript parity target.

Do in this chunk

1. Add the export for `WithPerlCertificateTrust` and inspect the generated TypeScript shape.
2. Preserve the fact that the API is still experimental on the C# side while keeping it in-scope for parity.
3. Keep the current installer-propagation and idempotency behavior intact.
4. Add the corresponding runtime and installer validation plan rather than silently deferring the method.

Stop and review when

1. The exported TypeScript story for certificate trust is explicit and consistent with the public C# surface.
2. The generated TypeScript API does not hide that the method is still experimental if that distinction still matters.

Exit criteria

1. `WithPerlCertificateTrust` is either intentionally exported with a validation plan or intentionally excluded with an explicit rationale.
2. The roadmap no longer treats certificate trust as an unnamed future item.

Likely blockers

1. The export itself is easy, but end-to-end validation may require more infrastructure than the method shape suggests.
2. The final TypeScript story still needs a clear way to communicate that the method is experimental without excluding it from parity.

[Back to roadmap chart](#roadmap-chart)

## Step 17: Validate certificate-trust behavior

Goal

1. Prove that TypeScript callers can rely on certificate bundle propagation for both runtime and installer flows.

Do in this chunk

1. Add a TypeScript scenario that exercises an HTTPS or CA-bundle-dependent flow.
2. Validate that runtime resources receive the expected certificate-related environment variables.
3. Validate that installer child resources also receive the propagated certificate trust settings.
4. Keep the scenario narrow enough that failures identify trust propagation problems rather than unrelated network complexity.

Stop and review when

1. The certificate-trust capability is backed by a concrete behavior test.
2. The scenario is simple enough that future regressions are diagnosable.

Exit criteria

1. The TypeScript AppHost suite contains at least one end-to-end certificate-trust proof.
2. Any platform-specific caveat is documented directly in the roadmap.

Likely blockers

1. Creating a realistic HTTPS or custom-CA test fixture may take more setup than the export work itself.
2. Cross-platform certificate bundle handling may require separate validation assumptions for Windows and Linux.

[Back to roadmap chart](#roadmap-chart)

## Step 18: Finish module and executable support in C#

Goal

1. Promote `AddPerlModule` and `AddPerlExecutable` from “builder exists” to “behavior is fully supported” before exposing them in TypeScript.

Do in this chunk

1. Identify the exact implementation gaps that currently make module and executable support feel incomplete.
2. Add or strengthen runtime, publish-mode, and integration coverage for those entry points on the C# side.
3. Decide whether both methods truly belong in the long-term supported surface or whether one should remain internal or explicitly unsupported.
4. Avoid exporting them to TypeScript until the C# story is no longer caveated.

Stop and review when

1. The underlying C# support decision is settled.
2. Reviewers can see that TypeScript export is no longer leading the C# implementation.

Exit criteria

1. `AddPerlModule` and `AddPerlExecutable` are either promoted to fully supported behavior or explicitly carved out of the parity target with written justification.
2. The roadmap no longer relies on vague “not fully implemented” language.

Likely blockers

1. The current behavior may be adequate for unit tests but not for realistic runtime or publish scenarios.
2. The supporting examples and validation fixtures may not exist yet.

[Back to roadmap chart](#roadmap-chart)

## Step 19: Export and validate module and executable entry points

Goal

1. Expose module and executable entry points to TypeScript only after step 18 confirms they are genuinely supported.

Do in this chunk

1. Add exports for `AddPerlModule` and `AddPerlExecutable` once their C# behavior is approved.
2. Inspect the generated TypeScript SDK to ensure the entry points read naturally and do not need polyglot-only reshaping.
3. Add at least one TypeScript AppHost scenario for the module flow and one for the executable flow.
4. Add automated validation that proves those entry points compile and run rather than merely appearing in `.modules/aspire.ts`.

Stop and review when

1. The generated SDK and the example behavior agree on what “supported” means.
2. Reviewers can validate both new entry points independently.

Exit criteria

1. `addPerlModule` and `addPerlExecutable` are present in the generated SDK only after their runtime behavior is proven.
2. The final TypeScript capability matrix includes behavior coverage for these entry points, not just export coverage.

Likely blockers

1. Example fixtures for module and executable flows may need to be created from scratch.
2. Publish-mode or deployment assumptions may still diverge from simple local run-mode validation.

[Back to roadmap chart](#roadmap-chart)

## Step 20: Close the parity gap

Goal

1. Finish the TypeScript parity work with a documented, test-backed answer to “what is fully covered now?”

Do in this chunk

1. Generate a final capability matrix that compares the intended supported C# Perl AppHost APIs to the generated TypeScript SDK surface.
2. Run the full Perl test project and the expanded TypeScript AppHost validation set.
3. Add any missing CI or platform-specific validation lane needed to keep the new capabilities from regressing.
4. Update the roadmap, support plan, README, and any sample docs so the supported TypeScript surface is unambiguous.
5. Record the residual exclusions, if any, as explicit product decisions rather than undocumented omissions.

Stop and review when

1. The supported TypeScript surface can be described in one precise list.
2. There is no remaining disagreement over whether the roadmap is complete.

Exit criteria

1. Every supported Perl TypeScript capability has generated-SDK coverage and the right level of behavior validation.
2. The final documentation matches the actual shipped TypeScript surface and the CI matrix can keep it that way.

Likely blockers

1. Linux-only or HTTPS-specific capabilities may require extra CI wiring before the parity claim is fully defensible.
2. One capability may still need a product decision even after the technical work is understood.

[Back to roadmap chart](#roadmap-chart)
