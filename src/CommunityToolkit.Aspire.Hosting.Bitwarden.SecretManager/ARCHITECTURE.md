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
   Publishing registers and runs a Bitwarden pipeline step per declared Bitwarden resource.
   Each step deploys the graph by creating or updating the Bitwarden project and managed secrets.

3. Reconciler as implementation detail:
   Reconciliation logic is the internal mechanism used by the publishing step (and local run path), not part of the public architecture contract.

4. Consumer contract parity:
   The user experience follows the Azure Key Vault style where declaration and references are first-class, and deployment materializes the declaration.

## Publishing

Publishing is the deployment moment for Bitwarden resources.
When you publish an AppHost, each declared Bitwarden resource contributes a
pipeline step via `WithPipelineStepFactory(...)` (a resource annotation-backed
step factory) rather than calling `Pipeline.AddStep(...)` directly.

Each step:

- has a resource-scoped name (`bitwarden-<resource-name>-reconcile`)
- is attached with `requiredBy: [WellKnownPipelineSteps.Deploy]`
- is tagged with `WellKnownPipelineTags.ProvisionInfrastructure`
- executes with `PipelineStepContext`
- resolves the matching `BitwardenSecretManagerResource`
- invokes `BitwardenSecretManagerReconciler.InitializeAsync(...)`

During execution, the step:

- resolves declared project and secret configuration
- connects to Bitwarden using configured credentials
- creates or updates the project
- creates or updates managed secrets
- records resulting identifiers/state needed by the runtime experience

Happy path:

1. Declare the Bitwarden project with `AddBitwardenSecretManager(...)`.
2. Declare any managed secrets with `AddSecret(...)`.
3. Reference the Bitwarden resource from dependent resources with `WithReference(...)` or reference a secret value with `WithBitwardenSecretValue(...)` or `WithBitwardenSecretId(...)`.
4. Publish the AppHost.
5. During pipeline execution, each Bitwarden step materializes its declared graph in Bitwarden.
6. The deployed graph is stable and available for consumers.

## Run Mode

For local run scenarios, the same declared graph is used. The implementation invokes reconciliation during resource initialization to keep local state aligned. This run-mode behavior is separate from publish-time step execution and does not change the architecture: declaration and pipeline-step deployment remain the primary model.

## State Management

The integration uses `IAspireStore` for all file-based state, consistent with Aspire's hosting conventions:

- **Reconciliation state** (`{safeResourceName}.{identityHash}.state.json`): persists the Bitwarden project ID and secret ID mappings between runs. Located in `{aspireStore.BasePath}/bitwarden/` by default.
- **SDK auth state** (`{safeResourceName}.auth.state`): caches the Bitwarden SDK authentication tokens between runs. Located in `{aspireStore.BasePath}/bitwarden/` by default.

Both paths are resolved at reconciliation time from `IAspireStore`, which the AppHost DI container provides. This works identically in run mode and publish mode.

`WithStateFile(...)` and `WithAuthStateFile(...)` are escape hatches that replace the default store-backed paths with an explicit location. These are intended for cases where state must be shared across workspaces or managed outside of Aspire's store (e.g. a shared CI cache).

## Non-Goals

- Defining a new custom manifest schema as the primary deployment contract.
- Using eventing subscribers as the deployment integration point for publishing.
- Making runtime reconciliation the primary architectural concept.

The intended design is pipeline-step-first, declared-resource-first.
