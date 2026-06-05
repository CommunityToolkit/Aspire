# Aspire Internal API Usage

This integration reaches into several experimental and private Aspire APIs to deliver its UX guarantees. This document explains each one: the problem it solves, why no public API covers it, what breaks if Aspire changes it, and how to detect breakage.

---

## Experimental APIs (diagnostic suppressions)

These are public APIs guarded by `[Experimental]` attributes. They are stable enough to ship on but carry an explicit "may change" signal.

### `ASPIREATS001` — `[AspireExport]`

**Files:** `BitwardenSecretManagerResource.cs`, `BitwardenSecretResource.cs`, `BitwardenSecretManagerExtensions.cs`

**What it does:** Registers types and methods with the Aspire Type System (ATS) so they are callable from polyglot apphosts (TypeScript, Python, etc.) via the JSON-RPC remote host. Both resource types carry `[AspireExport]` to register their type IDs. The exported extension methods (`AddBitwardenSecretManager`, `GetSecret`, `AddSecret`, `WithReference`, etc.) carry `[AspireExport]` or `[AspireExportIgnore]` to define the polyglot API surface. C# ergonomics overloads and methods with ATS-incompatible parameters (`EndpointReference`, generic callback types) are marked `[AspireExportIgnore]` with an explicit reason.

**Why needed:** Without `[AspireExport]` on extension methods, the integration has no ATS-callable surface and is inaccessible from non-C# apphosts. The ATS analyzer (ASPIREEXPORT008) also warns on extension methods that extend builder/resource types but lack either attribute — `[AspireExportIgnore]` documents a deliberate decision not to export, distinguishing it from an oversight.

**Breakage signal:** `ASPIREATS001` diagnostic stops compiling.

---

### `ASPIREPIPELINES001`, `ASPIREPIPELINES004` — Pipeline step registration

**Files:** `BitwardenSecretManagerExtensions.cs`, `BitwardenSecretManagerDeploymentStep.cs`

**What it does:** `ASPIREPIPELINES001` covers `WithPipelineStepFactory`, `WithPipelineConfiguration`, and the `PipelineStep` type, which are used to register the six Bitwarden pipeline steps per resource. `ASPIREPIPELINES004` covers `IPipelineOutputService` and `IComputeEnvironmentResource`, used by the env-file patch step to locate Docker Compose output directories and patch `.env.{env}` files after `prepare-{env}` writes them.

**Why needed:** The patch step is a workaround for Aspire's `PrepareAsync` (in `Aspire.Hosting.Docker`) only resolving `ParameterResource` and `ContainerImageReference` sources. Custom `IValueProvider` implementations like `BitwardenSecretResource` are skipped, leaving Bitwarden-derived env vars blank. The patch step fills those blanks after prepare runs. Remove it once Aspire handles all `IValueProvider` sources generically in `PrepareAsync`.

**Breakage signal:** Pipeline step registration diagnostics. More subtly: `IPipelineOutputService.GetOutputDirectory()` may change signature, or compute environment types may be renamed.

---

### `ASPIREPIPELINES002` — `IDeploymentStateManager`

**Files:** `BitwardenSecretManagerProvisioner.cs`

**What it does:** Reads and writes the per-environment deployment state file (`~/.aspire/deployments/{sha}/{env}.json`) that Aspire uses to persist parameter values across `aspire deploy` runs.

**Why needed:** The pre-sync step (`bitwarden-pre-sync-managed-{name}`) must run before `process-parameters` because the formal authentication step depends on `DeployPrereq`, which depends on `process-parameters` — there is no public way to insert a step after authentication but before the parameter prompt. The pre-sync step therefore performs inline authentication and writes resolved Bitwarden values (and prompted credential values) to the deployment state so that `process-parameters` finds them via `IConfiguration` and does not prompt the user again.

The deployment state file is loaded as a JSON configuration source at AppHost startup (`AddJsonFile(..., reloadOnChange: false)`). After writing values to the state and calling `IConfigurationRoot.Reload()`, the updated values are visible to `ParameterResource._lazyValue` when it is first evaluated by `ParameterProcessor.InitializeParametersAsync` in the `process-parameters` step.

**Breakage signal:** `ASPIREPIPELINES002` diagnostic stops compiling. State-file path or JSON structure changes would silently break the round-trip: written values might not be picked up by `IConfiguration` on reload.

---

### `ASPIREINTERACTION001` — `ParameterProcessor`

**Files:** `BitwardenSecretManagerProvisioner.cs`, `ParameterResourceExtensions.cs`

**What it does:** Drives the dashboard's parameter-resolution UI. Used for banner dismissal: after the provisioner resolves a secret value from Bitwarden in run mode, it calls `MarkParameterResolved` (which removes the parameter from `ParameterProcessor._unresolvedParameters` and cancels `_allParametersResolvedCts`). Without this, the "Parameters need values" banner stays open even though all values are now available.

**Why `SetParameterAsync` is not used for credential prompting:** `ParameterProcessor.SetParameterAsync` (available as `parameter.PromptAsync(...)`) calls `ValueInternal` internally to pre-fill the input form. `ValueInternal` evaluates `_lazyValue`, permanently caching `MissingParameterValueException` if the value is absent. After that call, no amount of saving to the deployment state and calling `IConfigurationRoot.Reload()` prevents `process-parameters` from re-prompting — the cached exception always re-throws. The pre-sync step uses `IInteractionService.PromptInputAsync` directly instead, which never touches `_lazyValue`.

**Breakage signal:** `ASPIREINTERACTION001` diagnostic stops compiling. The `ParameterProcessor` constructor signature or internal fields accessed via `MarkParameterResolved` may change.

---

## `UnsafeAccessor` workarounds

These access private members of `ParameterResource` and `ParameterProcessor` that have no public equivalent. If Aspire renames or removes any of these members, the AppHost will throw `MissingMethodException` or `MissingFieldException` at runtime. Run the AppHost against a new Aspire version before shipping a NuGet update.

---

### `get_WaitForValueTcs` — read `ParameterResource.WaitForValueTcs`

**File:** `ParameterResourceExtensions.cs`

**Why needed:** Three uses:

1. **`HasValue()`** — synchronously checks whether a parameter has already been resolved. `WaitForValueTcs` is set by `ParameterProcessor.InitializeParametersAsync`; if it is completed and non-empty the parameter has a value. If it is null, the lazy value-getter is invoked synchronously instead (with `MissingParameterValueException` caught and mapped to `false`).

2. **`ResolveWaitForValue(value)`** — called by the provisioner after resolving a secret from Bitwarden in run mode, to unblock any code awaiting `GetValueAsync()` without waiting for the dashboard prompt. `TrySetResult` is called only if the TCS is pending, so it never overwrites a value the user has already supplied interactively.

3. **`GetResolvedWaitForValue()`** — reads back the value stored by `PromptAsync` in the pre-sync step, after `InitializeWaitForValue()` pre-created the TCS (see `set_WaitForValueTcs` below).

**Breakage:** `MissingMethodException` at runtime.

---

### `set_WaitForValueTcs` — write `ParameterResource.WaitForValueTcs`

**File:** `ParameterResourceExtensions.cs`

**Why needed:** After the pre-sync step collects a credential via `IInteractionService.PromptInputAsync` and saves it to the deployment state, in-process callers within the same pre-sync execution (e.g. `ResolveAuthCachePathAsync` → `GetResolvedManagementAccessTokenAsync`) need the value before `IConfigurationRoot.Reload()` has been called. The step calls `InitializeWaitForValue()` to create the TCS, then immediately `ResolveWaitForValue(value)` to complete it with the collected value. Callers that read credentials via `GetValueAsync` then return the TCS result rather than attempting to re-prompt.

`GetResolvedWaitForValue()` is no longer used in this flow: the value is captured directly in the prompting code and passed to `ResolveWaitForValue`. The `GetResolvedWaitForValue()` helper is still present for symmetry but is currently unused.

**Why `_lazyValue` cannot be used instead:** `ParameterResource._lazyValue` is a `Lazy<string>` with `LazyThreadSafetyMode.ExecutionAndPublication` (the default), which permanently caches exceptions. If the lazy factory is evaluated before `process-parameters` creates the TCS and the config key is absent, it throws and caches `MissingParameterValueException`. All subsequent calls — including `ParameterProcessor.ProcessParameterAsync` after `Reload()` — re-throw the cached exception and never see the updated `IConfiguration` value. The pre-sync step therefore reads `IConfiguration` directly and never calls `HasValue()`, `ValueInternal`, or any path that evaluates `_lazyValue` on the parameters it is pre-resolving.

**Breakage:** `MissingMethodException` at runtime.

---

### `_unresolvedParameters` — `ParameterProcessor` private field

**File:** `ParameterResourceExtensions.cs`

**Why needed:** `ParameterProcessor.InitializeParametersAsync` adds parameters that throw `MissingParameterValueException` to `_unresolvedParameters`, then calls `HandleUnresolvedParametersAsync` which shows the "Parameters need values" banner and prompt loop. After the provisioner resolves a secret value from Bitwarden in run mode, the secret must be removed from this list. Without removal, the banner stays open and the prompt loop continues even though the value is now available. There is no public method to remove a specific parameter from the unresolved list.

**Breakage:** `MissingFieldException` at runtime. Banner stays open permanently for parameters resolved by Bitwarden.

---

### `_allParametersResolvedCts` — `ParameterProcessor` private field

**File:** `ParameterResourceExtensions.cs`

**Why needed:** `HandleUnresolvedParametersAsync` waits on a `CancellationToken` derived from `_allParametersResolvedCts`. When the last unresolved parameter is removed from `_unresolvedParameters`, cancelling this token causes the banner/prompt loop to exit immediately. Without cancellation, the loop continues until the user manually dismisses the banner, even if all parameters are now resolved.

**Breakage:** `MissingFieldException` at runtime. Banner dismissal is delayed; users must dismiss it manually.

---

## Upgrade checklist

When upgrading Aspire:

1. Check whether `ASPIREATS001`, `ASPIREPIPELINES001`, `ASPIREPIPELINES002`, `ASPIREPIPELINES004`, `ASPIREINTERACTION001` have moved from `[Experimental]` to stable — if so, remove the corresponding `#pragma` suppressions.
2. Run the AppHost and check for `MissingMethodException` / `MissingFieldException` from the `UnsafeAccessor` targets above.
3. Run `aspire deploy` end-to-end and verify that (a) managed secrets are not prompted when they exist in Bitwarden, (b) reference secrets are not prompted, and (c) the "Parameters need values" banner disappears automatically in run mode.
4. Check whether `DistributedApplicationBuilder.cs` still adds the deployment state file as a JSON configuration source (`AddJsonFile`), and whether the state file format produced by `FileDeploymentStateManager` is still compatible with the JSON configuration provider key format.
