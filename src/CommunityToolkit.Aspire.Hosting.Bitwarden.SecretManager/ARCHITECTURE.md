# Architecture

Bitwarden Secrets Manager is modeled as a declared AppHost resource graph.
The graph is the primary contract. Deployment happens through explicit Aspire pipeline steps that materialize the declared graph in Bitwarden.

- `BitwardenSecretManagerResource` declares a Bitwarden project and its configuration.
- `BitwardenSecretResource` declares a managed secret that belongs to that project.
- `IBitwardenSecretReference` declares a consumer-facing reference to a remote secret (by name or id), whether managed or existing.

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

Publishing is the deployment moment for Bitwarden resources.
When you publish an AppHost, each declared Bitwarden resource contributes four
pipeline steps via `WithPipelineStepFactory(...)`.

The steps run in order and are scoped to the resource by name:

| # | Step name | What it does |
|---|---|---|
| 1 | `bitwarden-authenticate-{name}` | Resolves credentials, loads the AppHost cache, authenticates with Bitwarden |
| 2 | `bitwarden-provision-project-{name}` | Creates or updates the remote Bitwarden project; binds the resolved project ID |
| 3 | `bitwarden-provision-secrets-{name}` | Creates or updates managed secrets, validates declared references, saves cache |
| 4 | `bitwarden-patch-env-{name}` | Patches Bitwarden-resolved values into Docker Compose `.env.{env}` files |

Steps 1–3 depend on `DeployPrereq`. Step 3 is tagged `ProvisionInfrastructure` and is required by `Deploy`. Because steps 1–3 carry no dependency on `prepare-{env}`, they can run concurrently with the Docker image prepare phase.

Step 4 is a Docker Compose workaround: `PrepareAsync` in `Aspire.Hosting.Docker` only resolves `ParameterResource` and `ContainerImageReference` sources, leaving Bitwarden-derived env vars blank. Step 4 patches those blanks after `prepare-{env}` runs and before `docker-compose-up-{env}` starts. It will be removed once the upstream issue is resolved.

Happy path:

1. Declare the Bitwarden project with `AddBitwardenSecretManager(...)`.
2. Declare any managed secrets with `AddSecret(...)`.
3. Reference the Bitwarden resource from dependent resources with `WithReference(...)` or reference a secret value with `WithBitwardenSecretValue(...)` or `WithBitwardenSecretId(...)`.
4. Publish the AppHost.
5. During pipeline execution, the four Bitwarden steps materialize the declared graph in Bitwarden.
6. The deployed graph is stable and available for consumers.

## Run Mode

For local run scenarios, the same declared graph is used. The implementation invokes reconciliation during resource initialization to keep local state aligned. This run-mode behavior is separate from publish-time step execution and does not change the architecture: declaration and pipeline-step deployment remain the primary model.

## Cache Files

The integration maintains two cache files on the AppHost, and one optional cache file in the deployed app.

### AppHost cache files (AppHost side)

Both files are resolved at reconciliation time from `IAspireStore`, which the AppHost DI container provides. They are used identically in run mode and publish mode.

- **AppHost cache** (`{safeResourceName}.{identityHash}.state.json`): the integration's own bookkeeping — persists the Bitwarden project ID and secret ID mappings between runs. Located in `{aspireStore.BasePath}/bitwarden/` by default. Override with `WithCacheFile(...)`.
- **AppHost auth cache** (`{safeResourceName}.auth-cache`): caches the Bitwarden SDK authentication session between runs so the AppHost does not need to re-authenticate on every run. Located in `{aspireStore.BasePath}/bitwarden/` by default. Override with `WithAuthCacheFile(...)`.

`WithCacheFile(...)` and `WithAuthCacheFile(...)` are escape hatches that replace the default store-backed paths with an explicit location. These are intended for cases where the cache must be shared across workspaces or managed outside of Aspire's store (e.g. a shared CI cache directory).

### App auth cache (deployed app side)

- **App auth cache**: caches the Bitwarden SDK authentication session inside the deployed app. This is independent of the AppHost auth cache — the two run in different processes and on different machines. Configure with `WithAuthCacheFile(...)` on the dependent resource builder (not on the Bitwarden resource), passing the Bitwarden source so the connection name can be resolved. Accepts a string for a fixed path or a parameter for an environment-specific path. The value is injected into the app via the `AuthCacheFile` configuration key under `Aspire:Bitwarden:SecretManager:{connectionName}`.

The AppHost reconciler never reads the app auth cache path. The deployed app never reads the AppHost cache files.

## Non-Goals

- Defining a new custom manifest schema as the primary deployment contract.
- Using eventing subscribers as the deployment integration point for publishing.
- Making runtime reconciliation the primary architectural concept.

The intended design is pipeline-step-first, declared-resource-first.

