# Archetype: sidecar and middleware infrastructure integration

Use this archetype for infrastructure that attaches to, discovers, or rewrites behavior for multiple resources: sidecars, component registries, service meshes, telemetry collectors, policy engines, and middleware processes.

Examples:

- Dapr-style sidecars plus component resources.
- OpenTelemetry Collector-style resources that route telemetry for multiple workloads.
- Middleware that adds environment variables, command-line arguments, generated config, or extra processes to other resources.

## Model shape

DO:

- Separate the target workload resource from sidecar, component, or middleware resources.
- Use annotations to attach sidecar/middleware configuration to target resources.
- Use explicit component/reference annotations when the middleware consumes other resources.
- Use global lifecycle hooks or event subscriptions to discover all relevant annotations and materialize derived resources.
- Register global hooks idempotently so repeated fluent calls do not duplicate derived processes or subscriptions.
- Keep component resources inert until lifecycle processing.

DON'T:

- Don't force sidecars or components into `IResourceWithParent<T>` when they are discovered and coordinated across the whole app model rather than owned by a single parent lifecycle.
- Don't require every consumer to manually wire repeated low-level environment variables if an annotation-driven middleware integration can derive them consistently.
- Don't let global scans mutate unrelated resources without a clear opt-in annotation.

## Annotation-discovery pattern

Use this pattern when fluent calls attach local configuration, but the integration needs the final app model before it can create sidecar processes, component manifests, generated files, or endpoint routing.

DO:

- Add small, typed annotations from `With{Feature}` or `With{Sidecar}` APIs instead of doing final wiring immediately.
- Register one global lifecycle hook or `IDistributedApplicationEventingSubscriber` idempotently, for example from `Add{Integration}`.
- In `BeforeStartEvent`, scan only resources with the opt-in annotation, collect component/reference annotations, and then materialize derived sidecar resources, generated config, or command arguments.
- Propagate relevant `WaitAnnotation`s from the target resource to the derived sidecar/process so dependency ordering remains correct.
- For resource-level aggregators such as local tunnel tools, store endpoint annotations on the aggregator resource and build config in `OnBeforeResourceStarted` after endpoint references are available.

Reference patterns:

- Dapr: `WithDaprSidecar` stores `DaprSidecarAnnotation`; `DaprDistributedApplicationLifecycleHook` subscribes to `BeforeStartEvent`, scans annotated resources, propagates waits, and materializes sidecar execution/config from the final model.
- Ngrok: `WithTunnelEndpoint` adds endpoint annotations to the `NgrokResource`; `OnBeforeResourceStarted` reads endpoint references and writes the ngrok config just before the container starts.

## Lifecycle and ordering

DO:

- Use `BeforeStartEvent` or equivalent lifecycle hooks when sidecars or middleware need the final app model before derived resources can be created.
- Propagate relevant `WaitAnnotation`s from the target resource to derived sidecar/process resources.
- Make target workloads wait for sidecars or setup resources only when the sidecar is required for correct startup.
- Use generated files or component manifests only from lifecycle callbacks where all referenced values are available.
- Branch carefully for publish/deploy; local sidecar processes often become deployment metadata instead of runnable resources.

DON'T:

- Don't create sidecar processes from constructors.
- Don't resolve endpoint host/port values before allocation.
- Don't cache global derived state without an invalidation path when inputs can change between runs.

## Configuration and generated files

DO:

- Prefer structured annotations for component metadata, secrets, endpoint values, and local paths.
- Generate config deterministically: stable filenames, stable ordering, no timestamps or random IDs unless required.
- Mount generated config files read-only where possible.
- Explain non-obvious lifecycle ordering in comments near the hook that materializes sidecars or config.

DON'T:

- Don't write secrets into generated config files unless the target tool has no secret-reference mechanism and the file is protected and run-only.
- Don't put deployment-only configuration in local run sidecar files.

## Observability and telemetry collectors

DO:

- Auto-wire telemetry only for resources that explicitly opt in through existing telemetry/exporter annotations.
- Keep collector health checks lightweight unless dependents require the collector to be protocol-ready.
- Avoid making telemetry forwarding a hidden hard dependency of application startup unless requested.

DON'T:

- Don't route all resources through a collector simply because they implement `IResourceWithEnvironment`; require a meaningful annotation or opt-in.
