# CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager

## Overview

Integrates Bitwarden Secrets Manager into your Aspire AppHost. Declare your Bitwarden project and secrets in the AppHost graph and apply them with `aspire deploy`.

## Getting Started

### Install the package

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager
```

### Basic setup

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

Use `WithExistingProject` to adopt a Bitwarden project that was created outside the AppHost graph, identified by its GUID.

```csharp
bitwarden.WithExistingProject(Guid.Parse("00000000-0000-0000-0000-000000000000"));
```

Use `WithApiUrl` and `WithIdentityUrl` to override the Bitwarden endpoints. Both default to the public Bitwarden cloud. For a self-hosted instance, pass an `ExternalServiceResource` to set the URL and wire up `WaitFor` in one call:

```csharp
var bitwardenApiServer = builder.AddExternalService("bitwarden-api", "https://bitwarden.example.com/api")
    .WithHttpHealthCheck("/alive");
var bitwardenIdentityServer = builder.AddExternalService("bitwarden-identity", "https://bitwarden.example.com/identity")
    .WithHttpHealthCheck("/alive");

bitwarden
    .WithApiUrl(bitwardenApiServer)
    .WithIdentityUrl(bitwardenIdentityServer);
```

When the URL varies by environment, use a parameter instead:

```csharp
var bitwardenApiUrl = builder.AddParameter("bitwarden-api-url");
var bitwardenApiServer = builder.AddExternalService("bitwarden-api", bitwardenApiUrl)
    .WithHttpHealthCheck("/alive");

bitwarden.WithApiUrl(bitwardenApiServer);
```

Use `WithCacheFile` to override the AppHost cache location. The cache tracks Bitwarden project and secret IDs between runs. Default is `.bitwarden/{name}.{env}.json` relative to the AppHost directory; relative paths resolve from there.

```csharp
bitwarden.WithCacheFile(".bitwarden/shared.Development.json");
```

Use `WithAuthCacheDirectory` to override the AppHost auth cache location. The auth cache persists the Bitwarden SDK session between runs to avoid login rate-limiting. Default is the Aspire store, named by the token UUID; relative paths resolve from there.

```csharp
bitwarden.WithAuthCacheDirectory("/ci/bitwarden-auth");
```

## Usage

Use `AddSecret(...)` to declare AppHost-owned secrets.

```csharp
// Aspire resource name and Bitwarden secret name are the same
IResourceBuilder<BitwardenSecretResource> managedSecret = bitwarden.AddSecret("api-key");

// Aspire resource name and Bitwarden secret name differ
IResourceBuilder<BitwardenSecretResource> managedSecret = bitwarden.AddSecret("api-key", remoteName: "API Key");
```

The value is resolved in this order during startup:

1. **Bitwarden upstream** — if the secret already exists, its current value is synced automatically. No prompt or configuration needed.
2. **Configuration** — reads `Parameters:{bitwardenResourceName}-{secretName}` (e.g. `Parameters:bitwarden-api-key`).
3. **Interactive prompt** — the dashboard prompts for the value. Once supplied, Bitwarden creates the secret.

Use `GetSecret(...)` to reference an externally owned secret that already exists in Bitwarden.

```csharp
// Aspire resource name and Bitwarden secret name are the same
IResourceBuilder<BitwardenSecretResource> existingSecret = bitwarden.GetSecret("api-key");

// Aspire resource name and Bitwarden secret name differ
IResourceBuilder<BitwardenSecretResource> existingSecret = bitwarden.GetSecret("api-key", remoteName: "API Key");
```

Use `WithReference(...)` to inject Bitwarden client configuration into dependent resources.

```csharp
builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden);
```

The injected configuration is under `Aspire:Bitwarden:SecretManager:{connectionName}` and includes `OrganizationId`, `ProjectId`, `AccessToken`, `ApiUrl`, and `IdentityUrl`.

By default the management token is injected. To supply a least-privilege read-only token instead:

```csharp
IResourceBuilder<ParameterResource> readOnlyToken = builder.AddParameter("bitwarden-readonly-token", secret: true);

builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden)
    .WithBitwardenAccessToken(bitwarden, readOnlyToken)
    .WithBitwardenAuthCacheDirectory(bitwarden, "/data/bitwarden"); // optional, use if you encounter login rate limits
```

> **Note:** The read-only token must be granted read permissions to the Bitwarden project manually — Bitwarden does not expose an API for this. Do this after the first AppHost run that creates the project.

To inject a secret ID for runtime fetching via the Bitwarden SDK:

```csharp
IResourceBuilder<BitwardenSecretResource> managedSecret = bitwarden.AddSecret("demo-api-key");

builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden)
    .WithBitwardenSecretId("DEMO_API_KEY_SECRET_ID", managedSecret.Resource);
```

To inject the resolved value directly (no SDK required in the app, but requires redeploy when the value changes):

```csharp
builder.AddProject<Projects.ApiService>("api")
    .WithBitwardenSecretValue("DEMO_API_KEY", managedSecret.Resource);
```

## Deployment

Run `aspire deploy`. The integration adds six pipeline steps per Bitwarden resource:

1. **Pre-sync managed secrets** — authenticates and fetches existing Bitwarden values for managed secrets before `process-parameters` runs. Prevents re-prompting for secrets that already exist.
2. **Authenticate** — resolves credentials and authenticates with Bitwarden Secrets Manager.
3. **Provision project** — creates or updates the remote Bitwarden project.
4. **Sync managed secrets** — reads upstream values for managed secrets whose local parameter values are missing.
5. **Provision secrets** — creates or updates managed secrets and validates declared references.
6. **Patch env files** — applies resolved values to Docker Compose environment files (Docker Compose deployments only).

## Reference

### Access tokens

| Token            | Set with                                                    | Used by            | Permissions needed      | When to use                                                              |
| ---------------- | ----------------------------------------------------------- | ------------------ | ----------------------- | ------------------------------------------------------------------------ |
| Management token | `AddBitwardenSecretManager(..., accessToken)`               | AppHost reconciler | Read + write to project | Always required                                                          |
| Client token     | `WithReference(bitwarden, bw => bw.WithAccessToken(token))` | Deployed app       | Read-only to project    | Supply a least-privilege token so the deployed app cannot modify secrets |

### Secret declarations

Both return `IResourceBuilder<BitwardenSecretResource>`. Access `.Resource` to pass to `WithBitwardenSecretValue` or `WithBitwardenSecretId`.

| API                           | What it does                                    | When to use                       |
| ----------------------------- | ----------------------------------------------- | --------------------------------- |
| `AddSecret(name)`             | AppHost-owned, read-write; names are the same   | Both names are the same           |
| `AddSecret(name, remoteName)` | AppHost-owned, read-write; names differ         | Aspire and Bitwarden names differ |
| `GetSecret(name)`             | Externally owned, read-only; names are the same | Both names are the same           |
| `GetSecret(name, remoteName)` | Externally owned, read-only; names differ       | Aspire and Bitwarden names differ |

### Secret references (injected into dependent resources)

| API                                               | What it injects                                                                           | When to use                                                     |
| ------------------------------------------------- | ----------------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| `WithReference(bitwarden)`                        | Connection config (`OrganizationId`, `ProjectId`, `AccessToken`, `ApiUrl`, `IdentityUrl`) | App uses the Bitwarden SDK to read secrets at runtime           |
| `WithBitwardenAccessToken(bitwarden, token)`      | Overrides the injected access token for this connection                                   | Supply a least-privilege read-only token                        |
| `WithBitwardenSecretId(envVar, secret)`           | Injects a secret ID as an env var; app fetches the value via the SDK at runtime           | Dynamic secret retrieval without redeploying when values change |
| `WithBitwardenAuthCacheDirectory(bitwarden, dir)` | Configures the app's Bitwarden SDK auth cache directory for this connection               | Persist auth session across restarts (process resources)        |
| `WithBitwardenAuthCacheVolume(bitwarden)`         | Mounts a named volume as the auth cache for this connection                               | Persist auth session across restarts (container resources)      |
| `WithBitwardenSecretValue(envVar, secret)`        | Injects the resolved secret value as an env var                                           | Simple injection; no Bitwarden SDK needed in the app            |

### Cache files

| Cache              | Format                            | Stores                          | Default                                                      | Override                                                                                                                | Relative paths    | When to override                                    |
| ------------------ | --------------------------------- | ------------------------------- | ------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------- | ----------------- | --------------------------------------------------- |
| AppHost cache      | JSON (integration-managed)        | Project ID + secret ID mappings | `.bitwarden/{name}.{env}.json` relative to AppHost directory | `bitwarden.WithCacheFile(path)`                                                                                         | AppHost directory | Share cache across AppHost projects or CI pipelines |
| AppHost auth cache | Encrypted (Bitwarden SDK-managed) | AppHost Bitwarden SDK session   | Aspire store, named by token UUID                            | `bitwarden.WithAuthCacheDirectory(path)`                                                                                | Aspire store      | Share session across CI runs                        |
| App auth cache     | Encrypted (Bitwarden SDK-managed) | App Bitwarden SDK session       | Not set — app re-authenticates each start                    | `WithBitwardenAuthCacheVolume(bitwarden)` (containers) or `WithBitwardenAuthCacheDirectory(bitwarden, dir)` (processes) | —                 | Persist app session across restarts                 |

### App auth cache

Without an auth cache the app re-authenticates with Bitwarden on every start, which triggers rate limiting under frequent restarts or rolling deployments.

**Named volume (containers)**

`WithBitwardenAuthCacheVolume` mounts a named volume and injects the path automatically. The volume survives restarts and is provisioned by the deploy tooling.

```csharp
builder.AddContainer("api", "myregistry/api")
    .WithReference(bitwarden)
    .WithBitwardenAuthCacheVolume(bitwarden); // volume: api-bitwarden-bitwarden-auth, path: /var/lib/bitwarden
```

Override the volume name or mount path when needed:

```csharp
api.WithBitwardenAuthCacheVolume(bitwarden, volumeName: "shared-bw-auth", containerDirectory: "/var/lib/bitwarden-shared");
```

> **Note:** `WithBitwardenAuthCacheVolume` requires a container resource and throws at startup for process resources (e.g. `AddProject`).

**Parameter (directory varies by environment)**

Use a parameter when the path differs between dev and production.

```csharp
IResourceBuilder<ParameterResource> authCacheDir = builder.AddParameter("bw-auth-cache-dir");

builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden)
    .WithBitwardenAuthCacheDirectory(bitwarden, authCacheDir);
```

Set the directory in user secrets for local development:

```json
{
    "Parameters": {
        "bw-auth-cache-dir": "/home/dev/.bitwarden"
    }
}
```

**Fixed string (same path everywhere)**

Use a string literal only when the app always runs as a container and the path is the same in all environments.

```csharp
builder.AddContainer("api", "myregistry/api")
    .WithReference(bitwarden)
    .WithBitwardenAuthCacheDirectory(bitwarden, "/home/app/.bitwarden");
```

> **Warning:** Do not pass a host-specific path to the string overload — the value is injected as-is and silently breaks in a container. Use a parameter when the path differs between machines or modes.

**When to use each**

| Scenario                                                                   | API                                                                                  |
| -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| App is a Docker container and you want persistent auth across restarts     | `WithBitwardenAuthCacheVolume(bitwarden)`                                            |
| App runs as a process in dev and as a container in production, dirs differ | `WithBitwardenAuthCacheDirectory(bitwarden, parameterBuilder)`                       |
| App is always a container and the directory is the same everywhere         | `WithBitwardenAuthCacheDirectory(bitwarden, string)`                                 |
| App is a process resource (`AddProject`)                                   | `WithBitwardenAuthCacheDirectory(bitwarden, string)` or parameter — no volume option |

### Resource states

The Bitwarden resource is a one-shot provisioner; dependent resources block on `WaitForCompletion` and start only when it reaches `Finished`.

Provisioning runs in four phases before `Running`:

1. **Authentication** — waits for the management access token, then authenticates. Fails fast so a bad token surfaces before you supply remaining values.
2. **Upstream managed secret sync** — resolves the project and reads existing Bitwarden values for managed secrets whose local parameter values are missing.
3. **Upstream reference secret sync** — fetches values for all `GetSecret` secrets. Fails here if a referenced secret does not exist in Bitwarden.
4. **Parameter collection** — waits for any remaining project name, organization ID, and managed secret values. `Running` is entered only once every value is in hand.

| State                  | Style   | Dependent resources          |
| ---------------------- | ------- | ---------------------------- |
| `NotStarted`           | —       | Blocked                      |
| `Waiting`              | —       | Blocked                      |
| `Running`              | —       | Blocked (still provisioning) |
| `Finished`             | Success | Unblocked — start normally   |
| `Exited` (exit code 1) | Error   | Error — fail to start        |

### Project provisioning decisions

Runs once per AppHost run during `bitwarden-provision-project`. Paths tried in order: explicit adoption → persisted mapping → create new.

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

Create new project. Use `WithExistingProject` to adopt a project created outside the declared graph.

### Managed secret provisioning decisions

Runs once per `AddSecret` secret during `bitwarden-provision-secrets`. Paths tried in order: explicit adoption → persisted mapping → name search.

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

### Unmanaged secret resolution

Runs once per `GetSecret` secret during `bitwarden-provision-secrets`. Read-only — no writes, no cache, no interactive prompt. Paths tried in order: explicit adoption → name search.

**Path A — explicit adoption (`WithExistingSecret`)**

| Secret found | In project | Outcome                            |
| ------------ | ---------- | ---------------------------------- |
| ✓            | ✓          | Sync secret value                  |
| ✓            | ✗          | Error: secret not in project       |
| ✗            | —          | Error: configured secret not found |

**Path B — name search**

| Name matches | Outcome                                                                                |
| ------------ | -------------------------------------------------------------------------------------- |
| 0            | Error: secret not found                                                                |
| 1            | Sync secret value                                                                      |
| > 1          | Error: duplicate names (resolve in Bitwarden or adopt by ID with `WithExistingSecret`) |

### Audit trail

Every time a managed secret is created or updated, the provisioner prepends a timestamped entry to the Bitwarden note field:

```
[2026-05-29T12:34:56Z] value changed (previous: old-value)
[2026-05-28T09:00:00Z] key renamed (previous: old-key), value changed (previous: initial-value)
[2026-05-27T08:00:00Z] Created
```

Each entry lists all fields that changed and their previous values. The trail is visible in the Bitwarden web vault and CLI alongside the current secret value.

## Compatibility

Tested with **Aspire 13.3.0**.

This integration relies on several experimental Aspire APIs (`ASPIREATS001`, `ASPIREPIPELINES001/002/004`, `ASPIREINTERACTION001`) and four `UnsafeAccessor` workarounds against private members of `ParameterResource` and `ParameterProcessor`. See [ASPIRE-INTERNALS.md](ASPIRE-INTERNALS.md) for the full explanation of each one, why no public API covers it, and what breaks when Aspire changes it.
