# Architecture

Bitwarden Secrets Manager is modeled as a declared AppHost resource graph.
The graph is the primary contract. Deployment happens through explicit Aspire pipeline steps that materialize the declared graph in Bitwarden.

- `BitwardenSecretManagerResource` declares a Bitwarden project and its configuration.
- `BitwardenSecretResource` declares either a managed secret (created or updated on every run, `IsManaged = true`) or a reference-only secret (read from an existing Bitwarden secret, `IsManaged = false`). Both modes inherit `ParameterResource` and are returned by `AddSecret` and `GetSecret` respectively as `IResourceBuilder<BitwardenSecretResource>`. `AddSecret` has two overloads: a single-name form where the Aspire resource name and the Bitwarden secret name are the same, and a two-name form (`name`, `remoteName`) where they differ. `GetSecret` has three: those same two name-based forms, plus a GUID form (`name`, `secretId`) that locates a specific secret by its Bitwarden identifier. The GUID form is an escape hatch for when multiple secrets in the project share the same name — Bitwarden does not enforce remote name uniqueness. To inject the resolved secret value into a dependent resource, pass the secret builder directly to `WithEnvironment`. To inject the secret ID instead, call `.AsSecretId()` on the builder and pass the result to `WithEnvironment`.
- Dependent resources must call `.WaitForCompletion(bitwarden)` explicitly to block until provisioning completes.

This design intentionally treats custom publish-manifest schema as legacy. The integration does not rely on a bespoke manifest payload as its architectural center.

## Architectural Principles

1. Declared graph first:
   AppHost resources are the source of truth.

2. Publish-time materialization:
   Publishing registers six Bitwarden pipeline steps per declared Bitwarden resource.
   The steps collectively deploy the graph by pre-syncing existing values, authenticating, provisioning the project, provisioning secrets, and patching environment files.

3. Provisioner as implementation detail:
   Provisioning logic is the internal mechanism used by the publishing steps (and local run path), not part of the public architecture contract.

4. Consumer contract parity:
   The user experience follows the Azure Key Vault style where declaration and references are first-class, and deployment materializes the declaration.

## Publishing

`aspire deploy` is the deployment moment for Bitwarden resources.
Each declared Bitwarden resource contributes six pipeline steps via
`WithPipelineStepFactory(...)`.

The steps run in order and are scoped to the resource by name:

| #   | Step name                               | What it does                                                                                                                                                                                        |
| --- | --------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | `bitwarden-pre-sync-managed-{name}`     | Prompts for any missing credentials, authenticates, and fetches existing managed secret values from Bitwarden; writes everything to the deployment state before `process-parameters` evaluates them |
| 2   | `bitwarden-authenticate-{name}`         | Resolves credentials, loads the AppHost cache, authenticates with Bitwarden                                                                                                                         |
| 3   | `bitwarden-provision-project-{name}`    | Creates or updates the remote Bitwarden project; binds the resolved project ID                                                                                                                      |
| 4   | `bitwarden-sync-managed-secrets-{name}` | Binds upstream values for managed secrets whose local parameter values are missing                                                                                                                  |
| 5   | `bitwarden-provision-secrets-{name}`    | Creates or updates managed secrets, validates declared references, saves cache                                                                                                                      |
| 6   | `bitwarden-patch-env-{name}`            | Patches Bitwarden-resolved values into Docker Compose `.env.{env}` files                                                                                                                            |

Step 1 must run before `process-parameters` because step 2 (`bitwarden-authenticate`) depends on `DeployPrereq`, which depends on `process-parameters` — there is no way to place a step that formally depends on authentication before the parameter prompt. Step 1 therefore performs its own inline authentication, prompting for any missing credentials via `ParameterProcessor` and saving them to the deployment state so `process-parameters` does not re-prompt for them. `process-parameters` is made to depend on step 1 (via `WithPipelineConfiguration`) so all values are written to the deployment state and reflected in `IConfiguration` before `ParameterProcessor` evaluates them.

Step 2 depends on `DeployPrereq`; steps 3–5 form a chain where each step depends on the previous. Steps 3–5 are tagged `ProvisionInfrastructure`. Step 5 is also required by `Deploy`. Because steps 2–5 carry no dependency on `prepare-{env}`, they can run concurrently with the Docker image prepare phase.

Step 6 is a Docker Compose workaround: `PrepareAsync` in `Aspire.Hosting.Docker` only resolves `ParameterResource` and `ContainerImageReference` sources, leaving Bitwarden-derived env vars blank. Step 6 patches those blanks after `prepare-{env}` runs and before `docker-compose-up-{env}` starts. It will be removed once the upstream issue is resolved.

Happy path:

1. Declare the Bitwarden project with `AddBitwardenSecretManager(...)`.
2. Declare any managed secrets with `AddSecret(...)`.
3. Reference the Bitwarden resource from dependent resources with `WithReference(...)` or `WithEnvironment(...)`.
4. Run `aspire deploy`.
5. During pipeline execution, the six Bitwarden steps materialize the declared graph in Bitwarden.
6. The deployed graph is stable and available for consumers.

## Run Mode

For local run scenarios, the same declared graph is used. The implementation invokes reconciliation during resource initialization (`OnInitializeResource`) to keep local state aligned. This run-mode behavior is separate from deploy-time step execution and does not change the architecture: declaration and pipeline-step deployment remain the primary model.

### State machine

The resource reports the following states during a run:

| State                  | Style   | Meaning                                                          |
| ---------------------- | ------- | ---------------------------------------------------------------- |
| `NotStarted`           | —       | Resource registered, initialization not yet started              |
| `Waiting`              | —       | Waiting for one or more parameter values (see phases below)      |
| `Running`              | —       | All values collected; actively provisioning project and secrets  |
| `Finished`             | Success | Provisioning succeeded; dependent resources may start            |
| `Exited` (exit code 1) | Error   | Authentication or provisioning failed; dependent resources error |

Dependent resources must call `.WaitForCompletion(bitwarden)` explicitly in apphost code. `Exited` with a non-zero exit code propagates the failure to dependents; `Finished` unblocks them.

### Four-phase parameter collection

Run-mode initialization collects parameters in four phases, all expressed as `Waiting` state to the dashboard until all inputs are ready:

**Phase 1 — authentication inputs only.**
The resource waits for the management access token (and the auth cache path, which is derived from it). It then authenticates with Bitwarden immediately. A bad or missing token fails fast here — before the user is asked for project name or secret values — and the resource transitions to `Exited` (exit code 1).

**Phase 2 — upstream managed secret sync.**
After a successful authentication, the resource resolves the project, then checks Bitwarden for existing managed secrets whose local parameter values are missing. For each secret that has an upstream value, the provisioner:

1. Binds the value into the resource's resolved-secret cache (`BindResolvedSecret`).
2. Calls `WaitForValueTcs.TrySetResult(value)` on the `BitwardenSecretResource`'s underlying `ParameterResource` so any caller awaiting `GetValueAsync()` unblocks immediately.
3. Removes the secret from `ParameterProcessor._unresolvedParameters` and cancels the prompt loop if the list becomes empty.
4. Publishes a `ResourceNotificationService` snapshot update to show the parameter state as `Running` in the dashboard.

The net effect: the "Parameters need values" banner disappears automatically after upstream sync for any secrets whose values already exist in Bitwarden.

**Phase 2.5 — upstream reference secret sync.**
After managed secret sync, the resource fetches current values for all reference-only secrets (declared via `GetSecret`). For each one, the provisioner binds the value and calls `ResolveWaitForValue` so `ParameterProcessor` sees the value and does not prompt. If a referenced secret does not exist in Bitwarden, the provisioner throws here rather than letting the dashboard ask for a value that the user cannot meaningfully supply.

**Phase 3 — remaining parameters.**
After upstream sync, the resource waits for any remaining project, organization, or managed secret parameter values. Managed secrets with no upstream value are still in `ParameterProcessor._unresolvedParameters` at this point, so the dashboard's interactive prompt remains active for those only. Once every value is available, the resource transitions to `Running` and provisioning continues.

The resource never enters `Running` while parameters are still pending. `Running` strictly means "all inputs gathered, provisioning in progress."

### Sync command

The "Reprovision" command repeats the full initialization sequence on demand. It is available in any state except `NotStarted`. Because parameter values are typically already resolved from the initial run, the resource moves through `Waiting` quickly before re-entering `Running`.

### BitwardenSecretResource as ParameterResource

`BitwardenSecretResource` inherits `ParameterResource`. Both managed (`IsManaged = true`, from `AddSecret`) and reference-only (`IsManaged = false`, from `GetSecret`) instances use this same type. The `IsManaged` flag drives provisioner dispatch and value-resolution behavior.

**`GetSecret` deduplication.** When `GetSecret` is called with a remote name that matches an existing secret (managed or previously registered reference-only), the same `BitwardenSecretResource` is returned — no second resource is created. The match is on `RemoteName`, so `AddSecret("app-key", "shared-secret")` followed by `GetSecret("shared-secret")` yields the same resource. This allows different parts of the AppHost to declare the write path and the read path independently without registering duplicates. `GetSecret(name, secretId)` bypasses this deduplication: it matches by `ExistingSecretId` instead, so it always produces a distinct resource even when another secret with the same remote name is already registered. Use this form when Bitwarden contains multiple secrets with the same name in the same project — Bitwarden does not enforce remote name uniqueness, so genuine duplicates can exist.

**Dashboard visibility.** Both kinds use `ResourceType = "Parameter"` in their initial snapshot so they appear in the Aspire dashboard parameters tab. For managed secrets, the `Source` property shows the configuration key (`Parameters:{resourceName}`) where a value can be pre-supplied. For reference-only secrets, the `Source` shows `Bitwarden: {remoteName}` (the Bitwarden secret name) to signal that the value comes exclusively from Bitwarden.

**`ParameterProcessor` integration.** Aspire's built-in `ParameterProcessor` processes every `ParameterResource` on startup. For **managed secrets**: the value getter throws `MissingParameterValueException` when no config key is set, so the secret is added to `_unresolvedParameters`; Phase 2 sync removes resolved secrets from that list. For **reference-only secrets**: the value getter returns `string.Empty` (never throws), so `ParameterProcessor` resolves the TCS immediately with an empty string and never adds the secret to `_unresolvedParameters`. The real value flows through `IValueProvider.GetValueAsync`, which reads from the Bitwarden resolved-secret cache populated by Phase 2.5.

**Value resolution order.** Both `IValueProvider.GetValueAsync(CancellationToken)` and `IValueProvider.GetValueAsync(ValueProviderContext, CancellationToken)` are explicitly overridden on `BitwardenSecretResource`. The context overload is necessary because `ParameterResource` implements it to call the public (non-interface) `GetValueAsync(CancellationToken)`, which goes directly to `WaitForValueTcs` and bypasses the Bitwarden cache. The override redirects the framework dispatch path through the interface implementation, which applies the priority below:

1. Bitwarden resolved-secret cache (populated by `BindResolvedSecret` after Phase 2/2.5 sync or Phase 4 provisioning).
2. **Managed only** — `ParameterResource.GetValueAsync` — waits on `WaitForValueTcs`, which is set by either `ParameterProcessor` (from config or user input) or by the provisioner (from upstream sync).
3. **Reference-only** — returns `null` if the Bitwarden cache is empty (pre-Phase 2.5); Phase 2.5 always populates the cache before the resource enters `Running`.

The Bitwarden cache always takes precedence because it represents the authoritative remote state. The `ParameterResource` fallback for managed secrets serves as the write path: the value the user or config supplies is what the provisioner pushes to Bitwarden when the secret does not yet exist.

## Deploy-mode parameter suppression

`process-parameters` (the Aspire built-in step that prompts for unresolved parameters) runs before `DeployPrereq` and therefore before all Bitwarden steps that depend on it. `bitwarden-authenticate` depends on `DeployPrereq`, so there is no way to formally place authentication before the parameter prompt. This creates a timing problem: managed secrets that exist in Bitwarden cannot be resolved through the normal pipeline ordering before `process-parameters` evaluates them, causing `aspire deploy` to prompt for values it could have fetched automatically.

The `bitwarden-pre-sync-managed-{name}` step addresses this by running before `process-parameters` (wired via `WithPipelineConfiguration`). It has no formal pipeline dependencies and performs its own inline authentication. It:

1. Reads any missing credentials (access token, organization ID) from `IConfiguration`. If a credential is absent and `IInteractionService` is available, prompts via `IInteractionService.PromptInputAsync` directly — **not** via `ParameterProcessor.SetParameterAsync` / `PromptAsync`, which calls `ValueInternal` and would permanently cache `MissingParameterValueException` in `_lazyValue` (see `_lazyValue` hazard below). After collecting, saves to the deployment state and sets `WaitForValueTcs` via `InitializeWaitForValue` + `ResolveWaitForValue` so in-process callers (`GetResolvedManagementAccessTokenAsync`, `GetResolvedOrganizationIdAsync`) see the value before `IConfiguration` is reloaded.
2. Authenticates with Bitwarden using the resolved credentials; looks up each managed secret's current value — by project ID from the AppHost cache when available, or by the `projectNameOrId` parameter if its current config value is a GUID — and writes each found value to the deployment state via `IDeploymentStateManager`. When neither a cached ID nor a GUID config value is available (project is identified by name only, first deploy), the step skips managed secret pre-sync; values are fetched after `bitwarden-provision-project` creates the project. When a project ID is known and the `projectNameOrId` parameter config value is absent, the step also fetches the project name from Bitwarden and pre-populates the parameter so `process-parameters` does not prompt for it. If the cached project ID no longer exists in Bitwarden (stale cache), a warning is logged and the secrets loop finds nothing; `bitwarden-provision-project` detects the missing project and creates a replacement.
3. Calls `IConfigurationRoot.Reload()` in a `finally` block to force the JSON configuration provider (which loaded the deployment state file at startup with `reloadOnChange: false`) to re-read the updated file. The `finally` ensures the reload happens even when the step exits early (e.g. interaction unavailable, cancelled prompt, Bitwarden auth failure after credentials were saved).

When `process-parameters` then calls `ParameterProcessor.InitializeParametersAsync`, each managed secret's `_valueGetter` reads `IConfiguration[key]` and finds the Bitwarden value — no prompt.

**`_lazyValue` hazard.** `ParameterResource._lazyValue` is a `Lazy<string>` with `LazyThreadSafetyMode.ExecutionAndPublication` (the default). This mode permanently caches exceptions: if the factory throws `MissingParameterValueException` on the first call, all subsequent calls re-throw the same cached exception, even after `IConfiguration` is reloaded. Therefore the pre-sync step must never call `HasValue()`, `ValueInternal`, or any path that evaluates `_lazyValue` on the parameters it is pre-resolving. The step reads `IConfiguration[key]` directly to check whether a local value is already present.

The first `aspire deploy` is prompt-minimizing: credentials are asked for once in step 1, and `process-parameters` finds them in `IConfiguration` and does not ask again. Managed secrets that exist in Bitwarden (even when the project ID is not yet cached) are pre-populated so `process-parameters` does not prompt for them either. Secrets that do not yet exist in Bitwarden still require a value — those are prompted by `process-parameters` as usual.

## Access Tokens

The integration uses two distinct access tokens with different scopes:

- **Management token** — supplied to `AddBitwardenSecretManager(...)`. Used exclusively by the AppHost provisioner to create and update the Bitwarden project and its secrets. It must have write permissions to the project.
- **Client token** — optionally supplied via `WithBitwardenAccessToken(bitwarden, token)` (chained after `WithReference`). Injected into the dependent resource as `AccessToken` under `Aspire:Bitwarden:SecretManager:{connectionName}`. Defaults to the management token when omitted.

The client token only needs read permissions to the project. Because Bitwarden does not expose an API for granting project access to a service account, this grant must be performed manually in the Bitwarden web vault. For a newly created project the grant must be done after the first AppHost run that creates the project.

The AppHost provisioner never reads the client token. The deployed app never reads the management token.

## URL Configuration

The API and identity URLs are stored on `BitwardenSecretManagerResource` as `ReferenceExpression` properties, initialized to the public Bitwarden cloud defaults:

```
ApiUrl      = https://api.bitwarden.com
IdentityUrl = https://identity.bitwarden.com
```

`WithApiUrl` and `WithIdentityUrl` each accept four forms:

- **String literal** — a fixed URL known at build time.
- **`ParameterResource`** — defers resolution until the parameter value is available. Intended for self-hosted instances whose URL varies by environment.
- **`ExternalServiceResource`** — extracts the URL (static or parameter-backed) from an `AddExternalService` resource and calls `WaitFor` automatically. Preferred for self-hosted instances because the external service also provides dashboard visibility and health checks.
- **`EndpointReference`** — points at an endpoint exposed by another resource in the AppHost. The Bitwarden resource calls `WaitFor` on that resource so authentication cannot start until it is running.

`ReferenceExpression` is used as the unified backing type because all four inputs are compatible with it and because it is the type Aspire expects when injecting values into dependent resources via `IValueProvider`. The provisioner, TLS validator, and `ApplyReferenceConfiguration` all resolve the URL through a single `GetValueAsync()` call regardless of which form was used.

The resolved URLs are published as `UrlSnapshot` entries in every `CustomResourceSnapshot` state update, so they appear as clickable links in the Aspire dashboard.

## Cache Files

The integration maintains two cache files on the AppHost, and one optional cache file in the deployed app.

### AppHost cache

The AppHost cache (`{resourceName}.{environment}.json` in `.bitwarden/`) is the integration's own bookkeeping file. It persists the Bitwarden project ID and secret ID mappings between runs so the AppHost can locate existing resources without querying Bitwarden on every start. Format: JSON, written and read by this integration.

Located in `.bitwarden/` relative to the AppHost directory by default, so it is naturally tracked in version control alongside the AppHost. Override with `WithCacheFile(...)`; relative paths resolve from the AppHost directory. Use the override to share the cache across multiple AppHost projects or to store it in a CI cache directory.

### AppHost auth cache

The AppHost auth cache persists the Bitwarden SDK authentication session between AppHost runs. Its primary purpose is to circumvent Bitwarden's rate limits on frequent logins: without the cache the AppHost would call the identity server on every run, which triggers rate limiting under rapid iteration or CI pipelines. Format: opaque encrypted text (Bitwarden `EncString`), written and read by the Bitwarden SDK — not plain JSON.

Located in `.bitwarden/` under the Aspire store by default, keyed by the UUID component embedded in the access token (the second `.`-delimited segment of `0.<uuid>.<secret>:<base64_key>`). The per-token filename means rotating the access token automatically produces a fresh cache file; the old one is silently abandoned.

Override with `WithAuthCacheDirectory(...)`; relative paths resolve from the Aspire store. Use the override to reuse a cached session across CI runs.

### Auth cache file internals

Both auth cache files (AppHost and app-side) are written and read exclusively by the Bitwarden SDK. The file on disk is a Bitwarden `EncString` — an AES-256-CBC-HMAC encrypted blob encoded as a single-line text string:

```
2.<base64-IV>|<base64-ciphertext>|<base64-HMAC>
```

The decrypted payload is JSON with three fields:

| Field            | Contents                                                            |
| ---------------- | ------------------------------------------------------------------- |
| `version`        | Format version integer (currently `1`)                              |
| `token`          | The JWT bearer token returned by the Bitwarden identity server      |
| `encryption_key` | The organization's symmetric encryption key used to decrypt secrets |

Source: [`crates/bitwarden-core/src/secrets_manager/state.rs`](https://github.com/bitwarden/sdk-internal/blob/aa8316827d1ea17f258b342ad2933286bcf2be1f/crates/bitwarden-core/src/secrets_manager/state.rs) in `bitwarden/sdk-internal`.

**Per-token isolation.** The file is encrypted using a key derived from the access token string itself. An access token has the structure `0.<uuid>.<client_secret>:<base64_key>`, and the embedded `<base64_key>` is HKDF-expanded into the AES-256-CBC-HMAC key that protects the cache file. Every distinct token string therefore produces a distinct encryption key. If a process tries to read a cache file written by a different token, AES-CBC-HMAC decryption fails and the SDK silently falls back to a fresh network authentication, overwriting the file — no crash, no error surfaced to the caller, just one extra round-trip.

The `bws` CLI uses this same convention: it names each state file after the token's UUID component (`~/.config/bws/state/<access_token_id>`). This integration follows suit, using the UUID from the second `.`-delimited segment of the access token string as the cache filename.

**Token rotation.** Rotating the access token changes the UUID, so the new token automatically gets a fresh cache file and the old one is never read again. However, the cached JWT inside the old file remains valid until its natural expiry (typically around one hour for Bitwarden). There is no server-side revocation check during cache file load — the SDK performs only a local expiry check against the stored JWT. This is Bitwarden SDK behaviour, not a gap in this integration.

### App auth cache

The app auth cache persists the Bitwarden SDK authentication session inside the deployed app. Its primary purpose is the same as the AppHost auth cache: circumventing Bitwarden's rate limits on frequent logins. Without it the app calls the identity server on every start. Format: opaque encrypted text (Bitwarden `EncString`), written and read by the Bitwarden SDK — not plain JSON. This is independent of the AppHost auth cache; the two run in different processes and on different machines.

The path is injected into the app via the `AuthCacheDirectory` key under `Aspire:Bitwarden:SecretManager:{connectionName}`. Three configuration paths exist:

All three configuration paths accept a **directory**; the filename within that directory is always `auth-cache`, managed by the integration.

**`WithBitwardenAuthCacheVolume(bitwarden)`** mounts a named Docker volume at `/var/lib/bitwarden` and sets the auth cache path to `/var/lib/bitwarden/auth-cache`. The volume name defaults to `{resourceName}-{connectionName}-bitwarden-auth` and can be overridden. Requires the destination to be a container resource. Preferred for container resources because no host-specific path is involved.

**`WithBitwardenAuthCacheDirectory(bitwarden, parameter)`** injects a parameter-backed directory path. The parameter resolves from user secrets or configuration in run mode, and the deploy tooling resolves it per environment. Use when the directory must differ between developer machines or deployment targets.

**`WithBitwardenAuthCacheDirectory(bitwarden, string)`** injects a fixed directory path. Safe only when the app always runs as a container and the directory is the same everywhere. Does not warn if a host-specific path is passed — that is a silent misconfiguration; use the parameter overload instead.

The AppHost reconciler never reads the app auth cache path. The deployed app never reads the AppHost cache files.

## Audit Trail

Every secret creation and update writes a timestamped audit entry to the Bitwarden secret's note field via `SecretUpdateAudit`. The record type owns three responsibilities:

- **Comparison** (`SecretUpdateAudit.Compare`) — derives which of value, key, and project changed by comparing the current remote state against the desired state. Captures the previous value for all three fields.
- **Guard** (`RequiresUpdate`) — short-circuits the update path when nothing changed, so no write is issued and the note is not mutated.
- **Note construction** (`PrependTo`, `CreationNote`) — `CreationNote` produces the initial `Created` entry. `PrependTo` builds the update entry from the set of detected changes, recording the previous value for each (`key renamed (previous: …)`, `project changed (previous: …)`, `value changed (previous: …)`), then prepends it to the existing note with a newline separator.

The note field is the only persistent record of what changed and when. It is stored in Bitwarden alongside the current value and is visible in the Bitwarden web vault and CLI.

## Non-Goals

- Defining a new custom manifest schema as the primary deployment contract.
- Using eventing subscribers as the deployment integration point for publishing.
- Making runtime reconciliation the primary architectural concept.

The intended design is pipeline-step-first, declared-resource-first.
