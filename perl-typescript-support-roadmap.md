# Perl TypeScript Support Migration Roadmap

This roadmap turns the existing support plan in [perl-typescript-support-plan.md](perl-typescript-support-plan.md) into reviewable execution chunks for adding TypeScript AppHost support to CommunityToolkit.Aspire.Hosting.Perl. The target is the current `examples/perl/cpanm-api-integration` scenario first, and the guiding rule is that the exported TypeScript surface should be idiomatic for TypeScript callers rather than a literal copy of every C# overload. Based on the current plan and the existing ATS patterns already used elsewhere in the repo, there is no structural reason this migration cannot be made; the main risks are export-shape compatibility, generated SDK ergonomics, and environment-level validation.

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
