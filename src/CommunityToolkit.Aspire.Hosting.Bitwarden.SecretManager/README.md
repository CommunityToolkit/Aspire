# CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager

## Overview

`CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager` helps you work with
Bitwarden Secrets Manager in your Aspire AppHost.

Use it to define your Bitwarden project and secrets in one place, then apply
them with `aspire deploy`.

## Getting Started

### Install the package

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager
```

### Basic setup

Create parameters for the project name, organization ID, and access token,
then add the Bitwarden resource to your AppHost. The Aspire resource name and
the Bitwarden project name are independent.

```csharp
IResourceBuilder<ParameterResource> organizationId = builder.AddParameter("bitwarden-organization-id");
IResourceBuilder<ParameterResource> accessToken = builder.AddParameter("bitwarden-access-token", secret: true);
IResourceBuilder<ParameterResource> projectName = builder.AddParameter("bitwarden-project-name");

IResourceBuilder<BitwardenSecretManagerResource> bitwarden = builder.AddBitwardenSecretManager(
    "bitwarden",
    projectName,
    organizationId,
    accessToken);
```

### Optional configuration

You can further customize the resource with the following options:

- `WithExistingProject(...)` adopts an existing Bitwarden project by
  identifier.
- `WithApiUrl(...)` and `WithIdentityUrl(...)` override the Bitwarden API and
  identity endpoints. Accepts a string, a parameter, an `ExternalServiceResource`,
  or an `EndpointReference`. Both default to the public Bitwarden cloud and are
  shown as clickable links in the Aspire dashboard.
- `WithCacheFile(...)` overrides the AppHost cache file location (default:
  `.bitwarden/{resourceName}.{environment}.json` relative to the AppHost
  directory). The AppHost cache tracks Bitwarden project and secret IDs
  between runs. Relative paths are resolved from the AppHost directory.
- `WithAuthCacheFile(...)` overrides the AppHost auth cache file location
  (default: Aspire store, keyed by a hash of the access token). The AppHost
  auth cache persists the Bitwarden SDK auth session between runs on the
  AppHost. Relative paths are resolved from the Aspire store.

For a self-hosted instance, model each endpoint as an `ExternalServiceResource`
and pass it directly. This sets the URL and wires up `WaitFor` in one call:

```csharp
var bitwardenApiServer = builder.AddExternalService("bitwarden-api", "https://bitwarden.example.com/api")
    .WithHttpHealthCheck("/alive");
var bitwardenIdentityServer = builder.AddExternalService("bitwarden-identity", "https://bitwarden.example.com/identity")
    .WithHttpHealthCheck("/alive");

bitwarden
    .WithApiUrl(bitwardenApiServer)
    .WithIdentityUrl(bitwardenIdentityServer);
```

When the URL varies by environment, use a parameter instead of a literal string:

```csharp
var bitwardenApiUrl = builder.AddParameter("bitwarden-api-url");
var bitwardenApiServer = builder.AddExternalService("bitwarden-api", bitwardenApiUrl)
    .WithHttpHealthCheck("/alive");

bitwarden.WithApiUrl(bitwardenApiServer);
```

## Usage

Use `AddSecret(...)` to declare managed Bitwarden secrets.

```csharp
IResourceBuilder<BitwardenSecretResource> managedSecret = bitwarden.AddSecret("api-key");
```

Each managed secret appears in the Aspire dashboard parameters tab. Its value is resolved in
this order during startup:

1. **Bitwarden upstream** — if the secret already exists in Bitwarden, its current value is
   synced automatically. No prompt, no configuration needed.
2. **Configuration** — if no upstream value is found, the secret reads the configuration key
   `Parameters:{bitwardenResourceName}-{secretName}` (e.g. `Parameters:bitwarden-api-key`).
3. **Interactive prompt** — if the configuration key is also absent, the dashboard prompts for
   the value. Once supplied, Bitwarden creates the secret with that value.

The dashboard parameter state transitions to `Running` as soon as the value is resolved by any
of these paths, so the "Parameters need values" banner disappears automatically after the
upstream sync phase completes for existing secrets.

Use `GetSecret(...)` to reference an existing remote secret.

```csharp
IResourceBuilder<BitwardenSecretResource> existingSecret = bitwarden.GetSecret("shared-api-key");
```

Both `AddSecret` and `GetSecret`
return `IResourceBuilder<BitwardenSecretResource>`, the difference is that `AddSecret` creates a managed secret that is synced and updated by Aspire, while `GetSecret` is unmanaged (read-only) and must already exist in Bitwarden.

Use `WithReference(...)` to inject Bitwarden client configuration into
dependent resources.

```csharp
builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden);
```

By default the management access token is injected into clients. To supply a
least-privilege read-only token instead, use `WithAccessToken` inside the
callback:

```csharp
IResourceBuilder<ParameterResource> readOnlyToken = builder.AddParameter("bitwarden-readonly-token", secret: true);

builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden, bw => bw.WithAccessToken(readOnlyToken));
```

> **Note:** The read-only token must be granted read permissions to the
> Bitwarden project manually in the Bitwarden web vault or CLI — Bitwarden
> does not expose an API for this, so it cannot be automated. For a newly
> created project, do this after the first AppHost run that creates the
> project.

Use `WithReference(bitwarden, bw => { ... })` to inject connection config and
apply additional Bitwarden-specific configuration in one call. The scoped
`bw` builder knows the connection name so you never repeat the source:

```csharp
IResourceBuilder<BitwardenSecretResource> managedSecret = bitwarden.AddSecret("demo-api-key");

// SDK approach: inject connection config + secret ID for runtime fetching
builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden, bw =>
    {
        bw.WithBitwardenSecretId("DEMO_API_KEY_SECRET_ID", managedSecret.Resource);
        bw.WithAuthCacheFile("/data/bitwarden/auth-cache"); // optional
    });
```

Use `WithBitwardenSecretValue(...)` to inject the resolved secret value
directly as an environment variable. No Bitwarden SDK required in the app,
but the app must be redeployed when the value changes:

```csharp
builder.AddProject<Projects.ApiService>("api")
    .WithBitwardenSecretValue("DEMO_API_KEY", managedSecret.Resource);
```

The injected configuration is available under
`Aspire:Bitwarden:SecretManager:{connectionName}` and includes:

- `OrganizationId`
- `ProjectId`
- `AccessToken`
- `ApiUrl`
- `IdentityUrl`

## Deployment

Deployment applies your declared Bitwarden resources.

Typical flow:

1. Declare the Bitwarden project and any managed secrets in the AppHost graph.
2. Run `aspire deploy` for the AppHost.

During `aspire deploy`, the integration runs five pipeline steps per Bitwarden
resource:

1. **Authenticate** — resolves credentials and authenticates with Bitwarden
   Secrets Manager.
2. **Provision project** — creates or updates the remote Bitwarden project.
3. **Sync managed secrets** — reads existing upstream values for managed
   secrets whose local parameter values are missing.
4. **Provision secrets** — creates or updates managed secrets and validates
   declared references.
5. **Patch env files** — applies resolved values to Docker Compose environment
   files (Docker Compose deployments only).

This keeps the experience declaration-first: resources and references are your
contract, and deployment materializes that contract.

## Reference

### Access tokens

| Token            | Set with                                                    | Used by            | Permissions needed      | When to use                                                              |
| ---------------- | ----------------------------------------------------------- | ------------------ | ----------------------- | ------------------------------------------------------------------------ |
| Management token | `AddBitwardenSecretManager(..., accessToken)`               | AppHost reconciler | Read + write to project | Always required                                                          |
| Client token     | `WithReference(bitwarden, bw => bw.WithAccessToken(token))` | Deployed app       | Read-only to project    | Supply a least-privilege token so the deployed app cannot modify secrets |

### Secret declarations

Both return `IResourceBuilder<BitwardenSecretResource>`. Access `.Resource` to pass the secret resource to `WithBitwardenSecretValue` or `WithBitwardenSecretId`.

| API               | What it does                                                                       | When to use                                                 |
| ----------------- | ---------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| `AddSecret(name)` | Declares a managed secret — value is written to Bitwarden on every run             | When Aspire owns the secret value                           |
| `GetSecret(name)` | References an existing remote secret — value is read from Bitwarden, never written | When the secret already exists and you only need to read it |

### Secret references (injected into dependent resources)

| API                                        | What it injects                                                                           | When to use                                                                           |
| ------------------------------------------ | ----------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| `WithReference(bitwarden)`                 | Connection config (`OrganizationId`, `ProjectId`, `AccessToken`, `ApiUrl`, `IdentityUrl`) | App uses the Bitwarden SDK to read secrets at runtime                                 |
| `WithReference(bitwarden, bw => { ... })`  | Connection config + scoped Bitwarden configuration via the callback                       | Also need `bw.WithAccessToken`, `bw.WithBitwardenSecretId`, or `bw.WithAuthCacheFile` |
| `WithBitwardenSecretValue(envVar, secret)` | The resolved secret value as an env var                                                   | Simple injection; no Bitwarden SDK needed in the app                                  |

### Cache files

| Cache              | Stores                          | Default                                                      | Override                                                         | Relative paths    | When to override                                    |
| ------------------ | ------------------------------- | ------------------------------------------------------------ | ---------------------------------------------------------------- | ----------------- | --------------------------------------------------- |
| AppHost cache      | Project ID + secret ID mappings | `.bitwarden/{name}.{env}.json` relative to AppHost directory | `bitwarden.WithCacheFile(path)`                                  | AppHost directory | Share cache across AppHost projects or CI pipelines |
| AppHost auth cache | AppHost Bitwarden SDK session   | Aspire store, named by token hash                            | `bitwarden.WithAuthCacheFile(path)`                              | Aspire store      | Share session across CI runs                        |
| App auth cache     | App Bitwarden SDK session       | Not set — app re-authenticates each start                    | `api.WithReference(bitwarden, bw => bw.WithAuthCacheFile(path))` | —                 | Persist app session across restarts                 |

### Resource states

The Bitwarden resource is a one-shot provisioner. Dependent resources use `WaitForCompletion`, so they block until provisioning finishes and then start.

Provisioning runs in four phases before the resource enters `Running`:

1. **Authentication** — waits only for the management access token, then authenticates with Bitwarden. Fails fast here so you learn about a bad token before providing the remaining values.
2. **Upstream managed secret sync** — resolves the project and reads existing Bitwarden values for managed secrets whose local parameter values are missing.
3. **Upstream reference secret sync** — fetches values for all reference-only secrets (declared via `GetSecret`). Fails here if a referenced secret does not exist in Bitwarden.
4. **Parameter collection** — waits for any remaining project name, organization ID, and managed secret values. The resource enters `Running` only once every value is in hand.

| State                  | Style   | Dependent resources          |
| ---------------------- | ------- | ---------------------------- |
| `NotStarted`           | —       | Blocked                      |
| `Waiting`              | —       | Blocked                      |
| `Running`              | —       | Blocked (still provisioning) |
| `Finished`             | Success | Unblocked — start normally   |
| `Exited` (exit code 1) | Error   | Error — fail to start        |

### Project provisioning decisions

Runs once per AppHost run, during the `bitwarden-provision-project` pipeline step.
Paths are tried in order: explicit adoption → persisted mapping → create new.

**Path A — explicit adoption (`WithExistingProject`)**

| Found in Bitwarden | Outcome                             |
| ------------------ | ----------------------------------- |
| ✓                  | Use configured project              |
| ✗                  | Error: configured project not found |

**Path B — persisted mapping exists in cache**

| Found in Bitwarden | Name matches configured | Outcome                                  |
| ------------------ | ----------------------- | ---------------------------------------- |
| ✓                  | ✓                       | Reuse persisted project                  |
| ✓                  | ✗                       | ⚠ Update project name (name drifted)     |
| ✗                  | —                       | ⚠ Create new project (persisted ID gone) |

**Path C — no cache**

Create new project. There is no name-search path here: the AppHost is the source of truth for the project, so a missing cache means a new project is created. Use `WithExistingProject` to adopt a project that was created outside the declared graph.

### Audit trail

Every time a managed secret is created or updated, the provisioner writes or prepends a timestamped entry to its Bitwarden note field:

```
[2026-05-29T12:34:56Z] value changed (previous: old-value)
[2026-05-28T09:00:00Z] key renamed (previous: old-key), value changed (previous: initial-value)
[2026-05-27T08:00:00Z] Created
```

Every change kind records its previous value: `key renamed (previous: …)`, `project changed (previous: …)`, `value changed (previous: …)`. When multiple fields change in a single update, all changes are listed in the same entry.

The audit trail grows at the top of the note on each update. It is visible in the Bitwarden web vault and CLI alongside the current secret value.

### Secret provisioning decisions

Runs once per managed secret, during the `bitwarden-provision-secrets` pipeline step.
Paths are tried in order: explicit adoption → persisted mapping → name search.

**Path A — explicit adoption (`WithExistingSecret`)**

| Secret found | Outcome                            |
| ------------ | ---------------------------------- |
| ✓            | Sync secret                        |
| ✗            | Error: configured secret not found |

**Path B — persisted mapping exists in cache**

| Secret found | In project | Outcome                     |
| ------------ | ---------- | --------------------------- |
| ✓            | ✓          | Sync secret                 |
| ✓            | ✗          | ⚠ Create replacement secret |
| ✗            | —          | ⚠ Create replacement secret |

**Path C — name search**

| Name matches | Historical rename | Outcome                                            |
| ------------ | ----------------- | -------------------------------------------------- |
| 0            | —                 | Create new secret                                  |
| 1            | ✗                 | Sync secret                                        |
| 1            | ✓                 | ⚠ Create new secret (local identity changed)       |
| > 1          | —                 | Prompt user to pick one (error if non-interactive) |

## Compatibility

Tested with **Aspire 13.3.0**.

This integration uses several experimental Aspire APIs and one `UnsafeAccessor`
workaround. These are summarized below so that upgrading Aspire is a conscious
decision rather than a silent breakage.

| Diagnostic / Mechanism                         | Files                                                                      | Members / Types                                                                                   | Why                                                                                                                                                                                                                                                   |
| ---------------------------------------------- | -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ASPIREATS001`                                 | `BitwardenSecretManagerResource`, `BitwardenSecretResource`                | `[AspireExport]`, `[AspireExport(ExposeProperties = true)]`                                       | Registers resource types with Aspire's typed export system so they appear in the dashboard and deployment manifest as first-class resources.                                                                                                          |
| `ASPIREPIPELINES001`, `ASPIREPIPELINES004`     | `BitwardenSecretManagerExtensions`, `BitwardenSecretManagerDeploymentStep` | `PipelineStepContext`, `IPipelineOutputService`, `IComputeEnvironmentResource`, `AddPipelineStep` | Hooks into `aspire deploy` to register five pipeline steps. The env-file patch step works around Aspire not calling `GetValueAsync` on custom `IValueProvider` sources during `prepare`, leaving Bitwarden-derived env vars blank in generated files. |
| `ASPIREINTERACTION001`                         | `BitwardenSecretManagerProvisioner`, `ParameterResourceExtensions`         | `ParameterProcessor`                                                                              | Triggers dashboard prompts for unresolved parameters and dismisses the "parameters need values" banner once Bitwarden resolves a secret value.                                                                                                        |
| `UnsafeAccessor` — `get_WaitForValueTcs`       | `ParameterResourceExtensions`                                              | `ParameterResource` private property                                                              | Synchronously checks whether a parameter has a value and resolves the `TaskCompletionSource<string>` to unblock `GetValueAsync` waiters after Bitwarden fetches the secret. No public equivalent.                                                     |
| `UnsafeAccessor` — `_unresolvedParameters`     | `ParameterResourceExtensions`                                              | `ParameterProcessor` private field                                                                | Removes a resolved parameter from the pending list so the dashboard banner reflects actual state. No public equivalent.                                                                                                                               |
| `UnsafeAccessor` — `_allParametersResolvedCts` | `ParameterResourceExtensions`                                              | `ParameterProcessor` private field                                                                | Cancels the banner `CancellationTokenSource` when all parameters are satisfied, dismissing it immediately. No public equivalent.                                                                                                                      |

If Aspire renames or removes any of the `UnsafeAccessor` targets, the integration will fail at
runtime with a `MissingMethodException` or `MissingFieldException`. Run the
AppHost against a new Aspire version and watch for those exceptions before
shipping a NuGet update.
