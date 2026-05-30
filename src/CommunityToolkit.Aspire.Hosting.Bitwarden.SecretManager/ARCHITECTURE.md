# Architecture

Bitwarden Secrets Manager is modeled as a declared AppHost resource graph.
The graph is the primary contract. Deployment happens through explicit Aspire pipeline steps that materialize the declared graph in Bitwarden.

- `BitwardenSecretManagerResource` declares a Bitwarden project and its configuration.
- `BitwardenSecretResource` declares either a managed secret (created or updated on every run, `IsManaged = true`) or a reference-only secret (read from an existing Bitwarden secret, `IsManaged = false`). Both modes inherit `ParameterResource` and are returned by `AddSecret` and `GetSecret` respectively as `IResourceBuilder<BitwardenSecretResource>`. Pass `.Resource` to `WithBitwardenSecretValue` or `WithBitwardenSecretId` to inject the secret into a dependent resource.

This design intentionally treats custom publish-manifest schema as legacy. The integration does not rely on a bespoke manifest payload as its architectural center.

## Architectural Principles

1. Declared graph first:
   AppHost resources are the source of truth.

2. Publish-time materialization:
   Publishing registers four Bitwarden pipeline steps per declared Bitwarden resource.
   The steps collectively deploy the graph by authenticating, provisioning the project, provisioning secrets, and patching environment files.

3. Provisioner as implementation detail:
   Provisioning logic is the internal mechanism used by the publishing steps (and local run path), not part of the public architecture contract.

4. Consumer contract parity:
   The user experience follows the Azure Key Vault style where declaration and references are first-class, and deployment materializes the declaration.

## Publishing

`aspire deploy` is the deployment moment for Bitwarden resources.
Each declared Bitwarden resource contributes five pipeline steps via
`WithPipelineStepFactory(...)`.

The steps run in order and are scoped to the resource by name:

| #   | Step name                            | What it does                                                                   |
| --- | ------------------------------------ | ------------------------------------------------------------------------------ |
| 1   | `bitwarden-authenticate-{name}`      | Resolves credentials, loads the AppHost cache, authenticates with Bitwarden    |
| 2   | `bitwarden-provision-project-{name}` | Creates or updates the remote Bitwarden project; binds the resolved project ID |
| 3   | `bitwarden-sync-managed-secrets-{name}` | Binds upstream values for managed secrets whose local parameter values are missing |
| 4   | `bitwarden-provision-secrets-{name}` | Creates or updates managed secrets, validates declared references, saves cache |
| 5   | `bitwarden-patch-env-{name}`         | Patches Bitwarden-resolved values into Docker Compose `.env.{env}` files       |

Steps 1–4 depend on `DeployPrereq`. Step 4 is tagged `ProvisionInfrastructure` and is required by `Deploy`. Because steps 1–4 carry no dependency on `prepare-{env}`, they can run concurrently with the Docker image prepare phase.

Step 5 is a Docker Compose workaround: `PrepareAsync` in `Aspire.Hosting.Docker` only resolves `ParameterResource` and `ContainerImageReference` sources, leaving Bitwarden-derived env vars blank. Step 5 patches those blanks after `prepare-{env}` runs and before `docker-compose-up-{env}` starts. It will be removed once the upstream issue is resolved.

Happy path:

1. Declare the Bitwarden project with `AddBitwardenSecretManager(...)`.
2. Declare any managed secrets with `AddSecret(...)`.
3. Reference the Bitwarden resource from dependent resources with `WithReference(...)` or `WithBitwardenSecretValue(...)`.
4. Run `aspire deploy`.
5. During pipeline execution, the four Bitwarden steps materialize the declared graph in Bitwarden.
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

Dependent resources declare `WaitForCompletion` on the Bitwarden resource. `Exited` with a non-zero exit code causes `WaitForCompletion` to propagate the failure to those dependents; `Finished` unblocks them.

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

**Dashboard visibility.** Both kinds use `ResourceType = "Parameter"` in their initial snapshot so they appear in the Aspire dashboard parameters tab. For managed secrets, the `Source` property shows the configuration key (`Parameters:{resourceName}`) where a value can be pre-supplied. For reference-only secrets, the `Source` shows `Bitwarden: {remoteName}` to signal that the value comes exclusively from Bitwarden.

**`ParameterProcessor` integration.** Aspire's built-in `ParameterProcessor` processes every `ParameterResource` on startup. For **managed secrets**: the value getter throws `MissingParameterValueException` when no config key is set, so the secret is added to `_unresolvedParameters`; Phase 2 sync removes resolved secrets from that list. For **reference-only secrets**: `IValueProvider.GetValueAsync` returns `null` before Phase 2.5 resolves the value, preventing `ParameterProcessor` from prompting; Phase 2.5 calls `ResolveWaitForValue` so the value is available by the time `ParameterProcessor` checks.

**Value resolution order.** `IValueProvider.GetValueAsync` is overridden on `BitwardenSecretResource`:

1. Bitwarden resolved-secret cache (populated by `BindResolvedSecret` after Phase 2/2.5 sync or Phase 4 provisioning).
2. **Managed only** — `ParameterResource.GetValueAsync` — waits on `WaitForValueTcs`, which is set by either `ParameterProcessor` (from config or user input) or by the provisioner (from upstream sync).
3. **Reference-only** — returns `null` if the Bitwarden cache is empty (pre-Phase 2.5); Phase 2.5 always populates the cache before the resource enters `Running`.

The Bitwarden cache always takes precedence because it represents the authoritative remote state. The `ParameterResource` fallback for managed secrets serves as the write path: the value the user or config supplies is what the provisioner pushes to Bitwarden when the secret does not yet exist.

## Access Tokens

The integration uses two distinct access tokens with different scopes:

- **Management token** — supplied to `AddBitwardenSecretManager(...)`. Used exclusively by the AppHost provisioner to create and update the Bitwarden project and its secrets. It must have write permissions to the project.
- **Client token** — optionally supplied via `WithReference(bitwarden, bw => bw.WithAccessToken(token))`. Injected into the dependent resource as `AccessToken` under `Aspire:Bitwarden:SecretManager:{connectionName}`. Defaults to the management token when omitted.

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

`ReferenceExpression` is used as the unified backing type because all three inputs are compatible with it and because it is the type Aspire expects when injecting values into dependent resources via `IValueProvider`. The provisioner, TLS validator, and `ApplyReferenceConfiguration` all resolve the URL through a single `GetValueAsync()` call regardless of which form was used.

The resolved URLs are published as `UrlSnapshot` entries in every `CustomResourceSnapshot` state update, so they appear as clickable links in the Aspire dashboard.

## Cache Files

The integration maintains two cache files on the AppHost, and one optional cache file in the deployed app.

### AppHost cache files (AppHost side)

- **AppHost cache** (`{resourceName}.{environment}.json` in `.bitwarden/`): the integration's own bookkeeping — persists the Bitwarden project ID and secret ID mappings between runs. Located in `.bitwarden/` relative to the AppHost directory by default, so it is naturally tracked in version control alongside the AppHost. Override with `WithCacheFile(...)`; relative paths resolve from the AppHost directory.
- **AppHost auth cache** (`{sha256(accessToken)}.auth-cache`): caches the Bitwarden SDK authentication session between runs so the AppHost does not need to re-authenticate on every run. Located in `.bitwarden/` under the Aspire store by default, keyed by a hash of the access token so that rotating the token automatically starts a fresh session. Override with `WithAuthCacheFile(...)`; relative paths resolve from the Aspire store.

`WithCacheFile(...)` and `WithAuthCacheFile(...)` are escape hatches that replace the default paths with an explicit location. These are intended for cases where the cache must be shared across multiple AppHost projects or stored in a CI cache directory.

### App auth cache (deployed app side)

- **App auth cache**: caches the Bitwarden SDK authentication session inside the deployed app. This is independent of the AppHost auth cache — the two run in different processes and on different machines. Configure via `WithReference(bitwarden, bw => bw.WithAuthCacheFile(...))`. Accepts a string for a fixed path or a parameter for an environment-specific path. The value is injected into the app via the `AuthCacheFile` configuration key under `Aspire:Bitwarden:SecretManager:{connectionName}`.

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

