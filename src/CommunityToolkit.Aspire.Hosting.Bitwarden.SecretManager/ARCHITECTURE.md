# Bitwarden secret integration architecture

The AppHost declares the Bitwarden project and its child secrets. The provisioner applies that graph during local startup or deployment.

## Resource and provider contracts

`BitwardenSecretResource` inherits `ParameterResource`. Its base getter and both `IValueProvider` calls read the same parameter state.
The resource also carries remote identity, its parent, and manifest references.

| Declaration | Value preparation | Remote key operation |
|---|---|---|
| `AddSecret(name)` | Configuration and defaults, then remote prefill for missing input, then normal prompting. | Create or update. |
| `AddSecret(name, expression)` | Explicit parameter override, otherwise a fresh expression read. | Create or update. |
| `GetSecret(name/id)` | Read the existing remote value. | Never write. |

Providers register their preparation work when the parent registers a secret. The provisioner does not classify sources with `AcceptsParameterInput`.
Input providers register for shared remote prefill. Reference providers register for input reset and deferred expression reads.
Unmanaged resources receive their parameter value when the shared lookup binds their remote identity.

The writer reads the prepared parameter. Authentication, duplicate resolution, key lookup, create/update calls, and ID bookkeeping remain shared.
A cached remote value is not an alternative input for a managed write.

## Deployment order

1. Clear previous generated and unmanaged values before parameter processing. Preserve explicit reference overrides.
2. Prefill missing ordinary inputs when deployment requires them.
3. Let Aspire process parameter inputs.
4. Authenticate and resolve the Bitwarden project.
5. Finish producer operations required by reference secrets.
6. Prepare each managed secret and apply the common write operation.
7. Validate external references and record secret IDs.
8. Patch deployment environment values after the target prepares its output.

The integration registers the existing `bitwarden-*` pipeline steps. The owning producer integration must order its output before `bitwarden-provision-secrets-{name}`.
A source expression records value dependencies but does not automatically create all deployment-step dependencies.

Pure `publish` and `build` targets clear generated state without remote prefill. They leave expression sources deferred.
A custom target that performs only artifact generation must preserve this same no-remote-operation boundary.

## Local startup

Resource initialization authenticates, resolves the project, prefills missing inputs, and reads external secrets.
Normal input collection completes before managed writes.
Reference preparation reads the selected source only when reconciliation reaches the managed write operation.

The parent does not report Running until managed writes finish. Consumers that wait for the parent cannot read a temporary empty reference value.
Reprovision uses the same preparation and write path.

## Reference preparation

An explicit `Parameters:{parent}-{secret}` override wins. Otherwise the provider resolves its expression again for each reconciliation.
The provider fills inherited parameter state and exposes failures through that same state.
The writer cannot fall back to an earlier result after source failure or cancellation.

The expression uses a `ValueProviderContext` whose caller is the Bitwarden resource. Every consumer receives that selected snapshot.
Keep direct resource references when consumers require different network addresses.

## Persistence

Bitwarden bookkeeping stores remote IDs and authentication cache locations. It does not supply generated values.
Ordinary inputs and explicit overrides can use Aspire's normal deployment-state persistence.
Generated output must not enter those input slots.

The resource setter does not persist values. Generic Aspire parameter processing can still save any populated parameter.
The pre-sync step therefore resets reference parameters before each processing pass and fills them after processing.
Do not call parameter processing again between reference preparation and the managed write.

The provider reads explicit configuration separately from current parameter state. A previous computed result is not an override.
Removing an override can require removing its saved deployment input as well as its active configuration setting.

## Compatibility boundary

`Extensions/ParameterResourceExtensions.cs` contains the Aspire internal accessors.
[Aspire PR 18108](https://github.com/microsoft/aspire/pull/18108) is a prerequisite for replacing them with a clean public-API implementation.
See [ASPIRE-INTERNALS.md](ASPIRE-INTERNALS.md).

On Aspire 13.5, processing an absent parameter produces an empty string. The integration relies on readiness and pipeline order until preparation finishes.
The inherited parameter and interface getters always agree. No separate expression getter bypasses the parameter.

## Validation

Tests cover builder shape, defaults, prefill, source overrides, missing and empty remote values, cancellation, and remote identity.
The suite also runs real Aspire parameter processing across repeated operations and the actual pipeline scheduler with a delayed producer.
Fake Bitwarden providers verify that the common writer receives the prepared value and never writes after a source failure.
