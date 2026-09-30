# Project v2 migration patterns

Use these mappings only after the AppHost is eligible, all required APIs are
available, and the exact resource/file changes are approved.

## C# project resources

### Generic metadata resource

Resolve the real `IProjectMetadata.ProjectPath`, including any
`AspireProjectMetadataTypeName` override.

```csharp
// Before
var api = builder.AddProject<Projects.Inventory_Api>("api");

// After: path is relative to the AppHost, derived from metadata rather than the type name
var api = builder.AddDotnetProject("api", "../Inventory.Api/Inventory.Api.csproj");
```

Preserve the resource variable, name, fluent calls, arguments, references, waits,
endpoints, health checks, replicas, and publishing configuration when supported.

### Path or directory resource

```csharp
// Before
builder.AddProject("api", "../Api");

// After, only after confirming the directory resolves to exactly one .csproj
builder.AddDotnetProject("api", "../Api");
```

Do not select among multiple projects heuristically.

### Launch profiles

The mappings are intentionally distinct:

```csharp
// Default profile selection
builder.AddProject<Projects.Api>("api");
builder.AddDotnetProject("api", "../Api/Api.csproj");

// Named profile
builder.AddProject<Projects.Api>("api", launchProfileName: "https");
builder.AddDotnetProject("api", "../Api/Api.csproj",
    options => options.LaunchProfileName = "https");

// Explicit legacy null means no launch profile in C#
builder.AddProject<Projects.Api>("api", launchProfileName: null);
builder.AddDotnetProject("api", "../Api/Api.csproj",
    options => options.ExcludeLaunchProfile = true);
```

Preserve existing `ProjectResourceOptions`, including
`ExcludeKestrelEndpoints`. Do not confuse an omitted argument with explicit null.

Because `AddDotnetProject` is experimental, wrap approved C# Project v2
declarations in a paired, local suppression unless the repository has an equally
narrow established convention:

```csharp
#pragma warning disable ASPIREDOTNETPROJECT001
var api = builder.AddDotnetProject("api", "../Api/Api.csproj");
#pragma warning restore ASPIREDOTNETPROJECT001
```

Do not add an unbounded file-wide disable or suppress unrelated diagnostics.

## TypeScript AppHosts

Use the generated 13.6 API in the configured AppHost entry point. Never edit
`.aspire/modules/`.

```typescript
// Before: omitted/null dispatch uses default options
const api = await builder.addProject("api", "../Api/Api.csproj");

// After
const api = await builder.addDotnetProject("api", "../Api/Api.csproj");
```

Legacy `addProject` keeps its existing generated wrapper shape. A named profile
is passed through `launchProfileOrOptions`:

```typescript
const api = await builder.addProject("api", "../Api/Api.csproj", {
  launchProfileOrOptions: "https"
});
```

The supported generated `addDotnetProject(name, path,
options?: DotnetProjectOptions)` API takes a flat third argument:

```typescript
const api = await builder.addDotnetProject("api", "../Api/Api.csproj", {
  launchProfileName: "https"
});
```

Do not nest the DTO under `options`. `DotnetProjectOptions` is specific to
`addDotnetProject`; it does not replace the shared `ProjectResourceOptions`
handle or change legacy `addProject` / `addCSharpApp` signatures.

The DTO can also represent proven excluded-profile and Kestrel behavior:

```typescript
await builder.addDotnetProject("worker", "../Worker/Worker.csproj", {
  excludeLaunchProfile: true,
  excludeKestrelEndpoints: true
});
```

Emit those flags only when the legacy source or already-resolved option-handle
state proves the same behavior. A legacy `ProjectResourceOptions` value remains
an RPC handle: inspect the source using its asynchronous property accessors
(`property.get()` / `property.set(...)`), not a JSON/object spread. Do not start
the AppHost during the read-only assessment just to query a handle. If the
effective values cannot be established, retain the resource and request a
decision.

`excludeKestrelEndpoints` controls whether Aspire derives resource-model
endpoints from the service's Kestrel configuration. It does not rewrite or
disable the service's own Kestrel configuration. Preserve that application
configuration unchanged unless a separate approved change requires otherwise.

If the resolved target API still requires a `ProjectResourceOptions` handle,
stop without edits. Do not invent a handle factory or support an older-preview
target through a special fallback; see the
[capability gate](compatibility-and-validation.md#eligibility-evidence).

Legacy polyglot omitted/null dispatch means default options, unlike C#'s
explicit-null string overload. `launchProfileOrOptions: null` therefore remains
default behavior, not evidence for `excludeLaunchProfile: true`. The new DTO
likewise treats omitted/null options, `{}`, and `launchProfileName: null` as
default selection. `excludeLaunchProfile: true` takes precedence over a supplied
name; `excludeKestrelEndpoints` is independent.

That null-options behavior belongs to the dispatcher, not the generated
TypeScript parameter type. Emit an omitted third argument or `{}` for a legacy
null wrapper; the third argument is optional but not nullable. The DTO property
`launchProfileName: null` is valid TypeScript.

## `AddCSharpApp`

For a `.csproj`, project directory, or file-based `.cs` resource:

```csharp
// Before
builder.AddCSharpApp("worker", "../Worker/Worker.csproj");

// After
builder.AddDotnetProject("worker", "../Worker/Worker.csproj");
```

Use the equivalent `addDotnetProject` mapping in TypeScript. Preserve the
legacy generated `addCSharpApp` signature; its options continue to use the
shared handle and do not gain `DotnetProjectOptions`. File-based apps require
.NET 10 or later. Preserve file directives and publishing choices. They do not
support `WithBuildEnvironment` or EF CLI operations.

## Blazor gateway

Use the specialized replacement:

```csharp
// Before
var gateway = builder.AddBlazorGateway("gateway")
    .WithBlazorClientApp(client, apiPrefix: "_api", otlpPrefix: "_otlp",
        proxyTelemetry: true);

// After
var gateway = builder.AddDotnetProjectBlazorGateway("gateway")
    .WithBlazorClientApp(client, apiPrefix: "_api", otlpPrefix: "_otlp",
        proxyTelemetry: true);
```

Use the generated `addDotnetProjectBlazorGateway` equivalent for TypeScript.
Preserve clients, prefixes, telemetry forwarding, service references, and
endpoints. Disclose before approval that the legacy gateway's explicit Dockerfile
publishing changes to the .NET SDK publishing pipeline.
Remove a block that only mutates the legacy gateway's
`DockerfileBuildAnnotation`; the Project v2 gateway does not create that
annotation. Preserve SDK image name, tag, and platform settings through the
existing `WithContainerBuildOptions`, and leave unrelated Dockerfile annotations
such as client-publish companions unchanged.
Also disclose the resolved framework/SDK, runtime/base image/OS and process user,
plus entrypoint, working-directory and port differences. Obtain specific approval
for material image changes; an approved API rename or SDK-publishing switch is not
blanket permission to upgrade the gateway runtime or change its user. If the
effective image policy is unknown, stop and request approved discovery rather
than assuming the AppHost/client framework or legacy Dockerfile defaults carry
over. See the [publishing boundary](compatibility-and-validation.md#publishing-boundaries).

## EF Core

Preserve supported `.csproj` migration operations, waits, and publish settings.
Project v2 integrations may expose a startup project through an interface rather
than a concrete `ProjectResource` property; update only supported call sites.

If `WithBuildEnvironment` or other custom MSBuild inputs affect the project,
explicitly disclose that `dotnet-ef` does not receive those values as global
properties. Runtime `WithEnvironment` is not an equivalent workaround.

Retain AppHost project references needed to generate migration-project or startup
metadata.

## Project-reference proof

Before removing an AppHost edge:

1. Map every `Projects.*` type to its declaring `ProjectReference`.
2. Search all AppHost source, generated metadata consumers, EF/Blazor setup, and
   conditional compilation paths.
3. Confirm every consumer belongs to the approved migrated set.
4. Retain the edge if any use is ambiguous, shared, or outside that set.

Do not modify project references inside service projects unless separately
approved; their library dependencies remain ordinary build dependencies.

## Fluent configuration checklist

Carry forward supported:

- `WithArgs` and application argument callbacks;
- `WithEnvironment` runtime callbacks;
- explicit endpoints and launch-settings-derived behavior;
- `WithReference`, endpoint references, `WaitFor`, and `WaitForCompletion`;
- health checks, replicas, parent/relationship annotations, and commands;
- manifest/publishing and container build options supported by Project v2.

Pause for a decision when an extension method is constrained to
`ProjectResource`, performs a concrete cast, or depends on a custom subtype.
