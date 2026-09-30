# Project v2 compatibility and validation

## Eligibility evidence

Resolve the selected AppHost's version from its own configuration:

| AppHost style | Evidence |
|---|---|
| Project-based C# | `Aspire.AppHost.Sdk`, `Aspire.Hosting.AppHost`, central package/version properties, imported props/targets, and resolved package graph |
| File-based C# | `#:sdk`, `#:package`, `#:property`, `global.json`, and resolved directives |
| TypeScript | configured AppHost entry point, package graph, Aspire config, and generated SDK/package resolution (read-only) |

The installed CLI, service target framework, or a neighboring AppHost is not
eligibility evidence. Stop without edits for `<13.6`, unknown, or inconsistent
versions. Never upgrade the AppHost as part of this migration.

Check the exact capabilities required by each candidate. The supported source
baseline includes both merged changes:

- [Project v2 publishing, EF, and Blazor support](https://github.com/microsoft/aspire/pull/19997).
- [Flat polyglot `DotnetProjectOptions`](https://github.com/microsoft/aspire/pull/20157).

[Commit `f856e006`](https://github.com/microsoft/aspire/commit/f856e006130459c4e11ed8cecd0b09e7e3873311)
contains both. This is source provenance, not a minimum published package version:
earlier 13.6 development builds can lack either capability. Verify the resolved
package/generated API or a known matching build; edit approval and package
declarations alone are not capability evidence.

## Unsupported and decision-required patterns

| Pattern | Required response |
|---|---|
| Azure Functions or unknown specialized `ProjectResource` subtype | Unsupported automatic migration. Preserve subtype-specific storage, launch, environment, references, and deployment. |
| F# `.fsproj` or VB `.vbproj` | Assessment only; do not treat Project v2 as a general replacement. |
| Direct `new ProjectResource(...)` | Manual design. `DotnetProjectResource` needs metadata, defaults, coordinated-build wiring, and publishing opt-in supplied by the builder API. |
| `GetProjectResources()`, casts, concrete generic constraints | Identify every call site and request a design decision. Do not replace wholesale with `IDotnetProgramResource`. |
| Custom publisher or image manager | Verify executable identity and `SupportsDotnetProgramPublishing()` capability; preserve or redesign only with approval. |
| Blazor gateway with changed or unresolved publishing defaults | Require explicit approval of resolved framework, SDK/base image/OS, and process-user changes. Generic API or SDK-publishing approval is insufficient; unknown values require approved discovery, not assumed parity. |
| File `.cs` app + build-only environment | Unsupported; do not invent an equivalent. |
| File `.cs` app + EF CLI | Unsupported. |
| EF + custom build properties | Warn that `dotnet-ef` does not receive those custom global properties. |
| Ambiguous project directory or metadata mapping | Stop; require an unambiguous path. |
| Build-only setting with unclear intent | Preserve current behavior and request approval before introducing `WithBuildEnvironment`. |
| Unsupported AppHost language | Assessment/manual guidance only. |
| TypeScript generated API exposes flat `DotnetProjectOptions` | Use the flat third argument for values proven from legacy source or resolved handle accessors. |
| TypeScript target API still requires a `ProjectResourceOptions` handle | Capability stop. Do not emit a plain object, silently discard options, or maintain an older-preview migration path. |

## Publishing boundaries

- Resources added through `AddDotnetProject` opt into .NET SDK publishing.
  Directly constructing the resource does not.
- Preserve supported `WithContainerBuildOptions` identity, destination, archive
  format, and platform settings.
- Preserve an explicit `PublishAsDockerFile()` override and its Dockerfile build
  ownership, including a callback's `WithDockerfile(contextPath, dockerfilePath)`.
  Dockerfile image identity uses the container callback's `WithImage(name, tag)`
  after `WithDockerfile`; `WithContainerBuildOptions.LocalImageName/LocalImageTag`
  control SDK publishing, not Dockerfile build identity.
  Preserve an existing prebuilt `ContainerImageAnnotation` applied through
  `WithAnnotation(..., ResourceAnnotationMutationBehavior.Replace)`: without a
  Dockerfile build annotation it must not acquire SDK build/push steps.
  Preserve any publish-only guard around that annotation: applying it in run mode
  can make DCP treat the project as a container for endpoint/target-port validation.
  Do not invent a project-specific `WithContainerImage`/`WithImage` overload or
  combine prebuilt ownership with `PublishAsDockerFile`. These supported cases
  are distinct from arbitrary custom publishers coupled to `ProjectResource`.
- The Project v2 Blazor gateway uses .NET SDK publishing rather than the legacy
  custom Dockerfile. Treat this as an approval-required intentional difference.
  Compare its effective target framework and SDK, runtime/base image and OS/platform,
  process user, working directory, entrypoint and ports before approving the change.
  The legacy image tag is selected from the AppHost runtime and the package's
  stamped image version; the v2 file app can instead follow the publishing SDK.
  A newer SDK can therefore change the gateway's runtime major version even when
  the AppHost/client project frameworks remain unchanged. Do not assume root and
  non-root images are interchangeable: writable paths, mounted files and privileged
  ports can be affected.
  If values or consequences are unresolved, retain the gateway pending a decision;
  do not silently choose a base, force root, edit the packaged script, or retarget it.
  A separately approved subset or owned Dockerfile publishing policy is a design
  alternative, not an automatic fallback. Do not use `WithBuildEnvironment` to
  control a file-based gateway: that API supports project files only.
  Its built-in gateway source already sets `PublishAot=false`; do not apply the
  generic file-app AOT warning to that gateway or change the user's client settings.
- File-based apps retain .NET SDK Native AOT defaults. Cross-operating-system
  publishing requires a target-OS build or explicit approval to set
  `PublishAot=false`; never disable AOT silently.
- Do not claim watch, hot reload, partial-run, or debugging improvements unless
  the targeted package actually implements and validation exercises them.

## Validation contract

Compare the actual edited application with a legacy baseline on the same
qualified toolchain where practical. Account for executable-based identity and
expected hidden coordinated-build resources, but do not broadly scrub the model.

| Area | Verify |
|---|---|
| Identity | user resource names and approved resource set |
| Source | normalized resolved project/file paths and working directories |
| Launch | default/named/excluded profile semantics and application arguments |
| Runtime | environment callbacks and values; evidence-backed option-handle values |
| Networking | endpoints, launch-settings/Kestrel behavior, external exposure; `excludeKestrelEndpoints` stops Aspire model derivation but does not disable service Kestrel configuration |
| Dependencies | service references, endpoint references, waits, completion waits |
| Scale and health | replicas, health checks, lifecycle relationships |
| Build | clean initial build, shared-library build correctness, required SDK |
| Publish | manifest inclusion, generated environment artifacts, selected pipeline, container options, and separately built image/archive |
| Approved gateway image changes | exact accepted framework/runtime, base/OS/platform and process user; entrypoint, directory and ports; all unapproved image settings unchanged |
| References | only proven-obsolete AppHost build edges removed |

Compilation must cover overload changes, not just creation calls.
`AddEFMigrations` on `IDotnetProgramResource` requires a narrow, paired
`ASPIREPROJECTS001` suppression in addition to `ASPIREDOTNETPROJECT001` on Project
v2 creation calls. Preserve necessary existing Blazor diagnostic scopes.

Run the repository's smallest relevant restore/build checks, then validate the
exact AppHost through Aspire:

```text
aspire start --non-interactive --isolated --apphost <path>
aspire wait <resource> --apphost <path> --non-interactive
aspire describe --apphost <path> --include-hidden --format Json --non-interactive
aspire stop --apphost <path> --non-interactive
```

Use orchestration and monitoring guidance for the installed CLI's exact syntax.
Do not substitute `dotnet run` for AppHost lifecycle or manual HTTP polling for
`aspire wait`.

When publish validation is approved, separate these stages:

| Stage | Check |
|---|---|
| Artifact generation | `aspire publish --apphost <path> -o <output> --non-interactive`; compare resource inclusion, configuration, endpoints, references, and target artifacts |
| Image build | `aspire do build --apphost <path> --non-interactive`; inspect the resulting image/archive and preserve build ownership |
| Deployment | Separate authorization; never implicitly push images or provision cloud/cluster resources |

Use `ASPIRE_CONTAINER_RUNTIME=podman` when Podman is the selected runtime; a
Docker shim is not required for Aspire's native Podman path. Validate only
task-owned resources and use distinct output directories/image tags.

Keep raw before/after image evidence. Exact equivalent paths such as `/app` and
`/app/` may be compared semantically, but never scrub runtime versions, image
identity or USER to force equality. With explicit approval, validate and report
the intended target image contract separately from unchanged behavior. A passing
probe does not prove unchanged deployment policy or authorize additional changes.

`--list-steps` avoids executing pipeline actions but still evaluates AppHost code,
step factories, and hosted services. It is not a read-only assessment shortcut.
Inspect custom behavior before running validation; use orchestration guidance to
stop the exact AppHost afterward, without affecting unrelated applications.

## Failure and idempotence

If restore, API lookup, compile, run, or publish validation fails:

- report the exact failed step and evidence;
- preserve migrated and unrelated user edits;
- do not fabricate a fallback API or success;
- do not weaken reference cleanup or disable AOT to make a check pass.

A second assessment after successful migration must produce no duplicate package
references, resources, diagnostic suppressions, or options.
