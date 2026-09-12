# GlitchTip hosting integration

Use this integration to model, configure, and orchestrate one GlitchTip project for an Aspire stack. Local development starts a private GlitchTip server. Deployment provisions the project in a shared GlitchTip instance supplied by the platform operator.

The integration targets .NET 10 and Aspire 13.5. The local image is GlitchTip 6.2.6; its management and artifact APIs are the compatibility target. Docker Compose is the supported target for automatic application-rollout ordering. The C# and TypeScript examples exercise the exported hosting APIs. Other deployment publishers require their own endpoint translation and rollout ordering support.

## Getting started

Add the package to your AppHost:

```shell
aspire add CommunityToolkit.Aspire.Hosting.GlitchTip
```

Local development requires a running Docker-compatible container runtime. Deployment also requires the Aspire Docker Compose integration and access to the shared GlitchTip management API.

## Usage example

```csharp
using CommunityToolkit.Aspire.Hosting.GlitchTip;

var builder = DistributedApplication.CreateBuilder(args);
builder.AddDockerComposeEnvironment("compose");

var project = builder.AddParameter("glitchtip-project", "my-stack");
var release = builder.AddParameter("release", "local");
var glitchtip = builder.AddGlitchTip("glitchtip", project, release);

var api = builder.AddProject<Projects.Api>("api")
    .WithGlitchTipHealthCheck("/health", endpointName: "http")
    .WithGlitchTipTelemetry(new GlitchTipTelemetryOptions
    {
        EnableLogs = true,
        EnableTracing = true
    })
    .WithReference(glitchtip);

if (builder.ExecutionContext.IsPublishMode)
{
    var healthUrl = builder.AddParameter("api-health-url");
    api.WithGlitchTipMonitorUrl(ReferenceExpression.Create($"{healthUrl}"));
}

builder.Build().Run();
```

The API project must expose the named HTTP endpoint and implement `/health`. Referencing GlitchTip supplies configuration; the consuming application still registers its SDK and instrumentation. See the [generic .NET client](../CommunityToolkit.Aspire.GlitchTip/README.md) and [ASP.NET Core client](../CommunityToolkit.Aspire.GlitchTip.AspNetCore/README.md).

The TypeScript AppHost uses the same default resource declaration:

```typescript
const project = await builder.addParameter("glitchtip-project", { value: "my-stack" });
const release = await builder.addParameter("release", { value: "local" });
const glitchtip = await builder.addGlitchTip("glitchtip", project, release);
await api.withGlitchTipReference(glitchtip);
```

The [TypeScript example](../../examples/glitchtip/GlitchTip.AppHost.TypeScript/apphost.mts) shows the full container-consumer setup.

### Project identity and ownership

A stack has exactly one `GlitchTipResource`. Reference that resource from every reporting project or container. Choose a stable project slug that no independently managed stack uses in the same organization. The slug is required and is used exactly as supplied: 1–50 lowercase ASCII letters, digits, hyphens, or underscores, beginning and ending with a letter or digit. Consecutive hyphens and the reserved name `new` are rejected before any API request because the released server would change them during creation. Use a display name for human-readable capitalization. The slug is not generated from an application name or normalized into a different slug.

Each startup or deployment looks up that project and creates it if absent. It resolves a reporting key named `aspire`, creating a key when needed. An explicitly inactive key is replaced. GlitchTip 6.2.6 omits the active flag from its key API, so the integration treats a returned key without that flag as usable; it cannot detect a key disabled outside the supported API. Delete the named key to request rotation on the next reconciliation. There is no DSN override, externally owned project mode, or durable DSN cache. An existing project at the configured identity is treated as Aspire managed.

In shared infrastructure, the instance, organization, and teams remain platform owned. The initial team is required configuration, but is only assigned during project creation. Subsequent deployments leave all team assignments unchanged, including membership in multiple teams. Changing the initial-team parameter does not move an existing project. Changing the project slug or organization selects another project; it does not move, rename, or delete the old project.

Use `glitchtip.WithProjectDisplayName(displayNameParameter)` to reconcile a human-readable name without changing the slug. Omitting this option preserves an existing display name.

The reporting environment is always the AppHost environment, such as `aspire deploy --environment production`. The default release is the required release parameter. Choose a build or image identifier in a deployment pipeline. A consumer can use `.WithGlitchTipRelease(serviceReleaseParameter)`; telemetry and that consumer's registered artifacts use the same effective release. The Aspire consumer resource name is the service identity.

### Local development, persistence, and MCP

Run the AppHost with `aspire start`. When another worktree already has an AppHost running, use `aspire start --isolated`. Each AppHost directory has its own GlitchTip server, PostgreSQL database, and storage; worktrees do not share one server through project-name prefixes.

The server runs in `all_in_one` mode with local email output, DuckDB storage, and uptime monitoring of private addresses enabled. PostgreSQL provides its cache and background task queue; local development does not start Redis or Valkey. It creates a local administrator (`admin@example.com` by default), organization `Aspire` (slug `aspire`), and initial team `aspire`. Parameter names based on the resource name allow configuring the local administrator email, organization slug, and initial team. Generated administrator and PostgreSQL passwords and the GlitchTip secret key are persisted Aspire secret parameters scoped to the AppHost directory. Keep those secrets with their associated persisted volumes.

Open the server resource's **GlitchTip** dashboard link. Its setup log identifies the administrator email and password parameter names without printing their values. MCP is enabled locally; use the **MCP (authenticate in your client)** link and complete authentication in your MCP client. Enabling the endpoint does not grant an anonymous client access.

Two Docker volumes persist local state:

| Volume | Contents |
| --- | --- |
| `{resource}-{scope}-postgres` | Database, users, projects, keys, cache, and background tasks |
| `{resource}-{scope}-uploads` | Uploaded files and DuckDB cold storage |

`scope` is a hash of the absolute AppHost directory. Stopping Aspire retains the volumes. Moving the checkout to another directory selects a different storage scope.

For a deliberate local reset, stop this AppHost, identify its exact two volume names in Docker, and remove those two volumes together. This deletes that local instance's data. Start the AppHost to create an empty instance using its persisted secret parameters. Do not remove another worktree's volumes. There is no automatic reset or shared-server deletion during teardown.

### Publish and deploy

`AddGlitchTip(name, projectSlug, release)` configures both modes. Local execution starts its private instance; the shared-server parameters are not declared locally and do not prompt for values. For publish and deploy, it automatically declares these required parameters; replace `{name}` with the resource name, such as `glitchtip`:

| Parameter | Value |
| --- | --- |
| `{name}-url` | The shared GlitchTip base URL |
| `{name}-organization` | An existing organization slug |
| `{name}-initial-team` | An existing team slug, used only when creating the project |
| `{name}-api-token` | A secret management API token |

Supply these values through Aspire's normal parameter and secret configuration in the deployment environment. The automatic names are reserved; do not add another parameter resource with the same name. The token needs access to look up and create projects and reporting keys, reconcile uptime monitors, and upload and read the registered release artifacts. It is not forwarded to reporting services. Neither these parameters nor their values connect local execution to the shared instance.

If the AppHost defines parameters for the shared instance under different names, bind those instead with the optional `WithDeploymentParameters` override. Keep declarations of custom deployment-only parameters inside the publish-mode branch so local execution does not prompt for them:

```csharp
if (builder.ExecutionContext.IsPublishMode)
{
    glitchtip.WithDeploymentParameters(
        builder.AddParameter("observability-url"),
        builder.AddParameter("observability-organization"),
        builder.AddParameter("observability-initial-team"),
        builder.AddParameter("observability-api-token", secret: true));
}
```

The override uses the supplied bindings and removes unused automatic deployment parameters. Local execution still uses its private instance. The API-token parameter must be marked secret. Parameters created by your own AppHost remain your responsibility; the override does not remove caller-owned parameters.

The equivalent TypeScript override is:

```typescript
if (await builder.executionContext().isPublishMode()) {
    await glitchtip.withDeploymentParameters(
        await builder.addParameter("observability-url"),
        await builder.addParameter("observability-organization"),
        await builder.addParameter("observability-initial-team"),
        await builder.addParameter("observability-api-token", { secret: true }));
}
```

`aspire publish` generates the Docker Compose assets with unresolved reporting references. It does not contact GlitchTip, create a project, reconcile monitors, or upload files. The local GlitchTip and PostgreSQL resources are excluded from published infrastructure. Pure publish output is not a substitute for the deployment pipeline's DSN resolution.

`aspire deploy` resolves the project and a fresh DSN before Compose prepares its deployment environment. After the application build and Compose preparation, it reconciles monitors and uploads and verifies registered artifacts before Compose starts the application services. Use the Compose publisher's output-directory options for its generated assets; GlitchTip does not maintain a separate credential cache. Protect generated deployment environment files, which contain reporting DSNs. A deployment may hand the freshly resolved DSN to its existing secret store as an output. That stored output must never replace a fresh management-API resolution on a subsequent deployment.

The pipeline also exposes `glitchtip-provision-{resourceName}` and `glitchtip-synchronize-{resourceName}`. The latter depends on provisioning and the build step. A management-only AppHost can use these steps when another tool owns application rollout, provided the owning pipeline orders them before its handoff and supplies monitor-facing URLs explicitly. No Docker runtime is required by the management API calls themselves. This does not add automatic rollout ordering for a non-Compose compute environment.
An unavailable management API, rejected credentials, unresolved project/key, invalid monitor declaration, or required artifact failure blocks deployment. There is no stale-DSN fallback. If this dependency can block an emergency deployment, register GlitchTip and its consumer references conditionally in your own AppHost. Local project setup failures also block dependent consumers from starting.

After services start, reporting delivery failures do not change application readiness. The client integration registers no GlitchTip readiness check. An unreachable monitor target is a runtime uptime result; deployment does not wait for that target to become healthy. Stopping or removing the stack does not delete the shared project, its retained events, or artifacts.

### Uptime monitors from Aspire health checks

`WithGlitchTipHealthCheck` adds a normal Aspire HTTP health check and retains its endpoint, path, and expected status for monitoring. GlitchTip monitors are discovered only on resources that reference the GlitchTip project. Public Aspire `EndpointProbeAnnotation` metadata is also recognized, with expected status 200 and its declared timing when available.

Aspire 13.5 does not expose the endpoint and path of an ordinary `WithHttpHealthCheck` through public annotations. Use `WithGlitchTipHealthCheck` for automatic mapping of those checks; arbitrary health-check delegates cannot be converted into HTTP monitors. No reflection is used to recover private Aspire metadata.

Each monitor performs an unauthenticated HTTP GET. Custom authentication headers are not supported. Make the health endpoint reachable under that contract or disable its monitor while keeping the Aspire health check:

```csharp
api.WithGlitchTipMonitor("/health", enabled: false);
```

The defaults are a 60-second interval and 20-second timeout. Set stack defaults with `glitchtip.WithMonitorDefaults(new GlitchTipMonitorOptions { ... })` or override a declared check:

```csharp
api.WithGlitchTipMonitor("/health", options: new GlitchTipMonitorOptions
{
    IntervalSeconds = 120,
    TimeoutSeconds = 10
});
```

Local monitor URLs resolve from the local GlitchTip container's network context. Compose endpoint references use the deployed service hostname and internal port. GlitchTip 6.2.6 rejects bare service names such as `http://api:8080/health`, even when private-address monitoring is enabled. Supply a complete monitor-facing URL with a fully qualified hostname or IP address for normal Compose deployments. That address must actually resolve and be reachable from the shared GlitchTip instance; the integration does not invent DNS aliases:

```csharp
var healthUrl = builder.AddParameter("api-health-url");
api.WithGlitchTipMonitorUrl(ReferenceExpression.Create($"{healthUrl}"));
```

This value can come from platform configuration or an endpoint expression. Aspire resolves the declaration; it does not discover reverse-proxy routes, expose the service publicly, or probe the URL from the deployment machine. Server-side restrictions on private addresses still apply on a shared instance.

Monitors are always Aspire managed, with identity derived from the project, AppHost environment, resource, endpoint, and path. Reconciliation restores declared settings and removes obsolete managed monitors for that project/environment after all desired updates succeed. It preserves manual monitors and monitors for other environments. Avoid editing the managed identity in a monitor's name. Undeclared response-body and confirmation-threshold settings are preserved on updates.

### Source maps and debug symbols

Register only build outputs that belong to a reporting resource:

```csharp
api.WithGlitchTipSourceMaps("../Web/dist")
   .WithGlitchTipDebugSymbols("../Api/bin/Release/net10.0/publish");
```

Paths are files or directories, resolved relative to the AppHost directory. The deploy machine must have those outputs after the build step; files that exist only inside a container image are not extracted automatically. Registration scans only the selected subtree and skips directory reparse points. Uploads run during deployment. Pure publish does not upload artifacts.

Local uploads are off by default. Enable them on the GlitchTip resource when the registered output files are ready before local startup:

```csharp
glitchtip.WithLocalArtifactUploads();
```

The TypeScript equivalent is `await glitchtip.withLocalArtifactUploads();`. Local setup uploads and verifies the same registrations before releasing dependent resources to start, including resources excluded from deployment. Required artifact failures block those resources from starting. `WithLocalArtifactUploads(enabled: false)` disables the local behavior without changing deployment. The integration does not build, extract, inject debug IDs into, or watch local outputs. Prepare the files before `aspire start`, and restart the AppHost after rebuilding artifacts to reconcile changed output. Existing identical artifacts are reused.

JavaScript must already contain build-time debug IDs and a local relative `sourceMappingURL`. Each JavaScript/source-map pair must have the same unique UUID, and its map must remain inside the registered directory. Prepare the final emitted JavaScript with a compatible build plugin or `glitchtip-cli sourcemaps inject` before startup or deployment and ship those same files to browsers. The integration packages and uploads the prepared output; it does not rewrite the JavaScript or build source maps. Supported script extensions are `.js`, `.mjs`, `.cjs`, `.jsbundle`, and `.bundle`.

Native registrations recognize Mach-O files in dSYM bundles, Windows PDB, portable PDB, and ELF artifacts by their file signatures. Generate suitable debug information during the application build. The server must support processing the registered format. Universal (fat) Mach-O archives are rejected before upload because GlitchTip 6.2.6 does not preserve every architecture in one archive. Extract and register separate thin debug files for each architecture; marking the registration optional does not bypass this check.

Required missing or empty registrations, malformed source maps, rejected uploads, and assembly/verification timeouts fail deployment or opted-in local setup. Use `optional: true` only when absence is expected; it is not an ignore-errors switch. Identical artifacts already present on the server are reused. Uploads verify persisted source/map checksums and debug IDs, or native checksums and server-reported debug IDs, after assembly. This confirms server processing, not the symbolication of a live exception. No retention or remote artifact cleanup policy is applied.

## Connection Properties

The `GlitchTipResource` exposes this connection property:

| Property | Format |
| --- | --- |
| `Uri` | Sentry-compatible reporting DSN, `http[s]://{public-key}@{host}[:port]/{project-id}` |

The GlitchTip-specific `WithReference` supplies `ConnectionStrings__{resourceName}` and `SENTRY_DSN`, plus `SENTRY_ENVIRONMENT` and `SENTRY_RELEASE`. It also supplies `Aspire__GlitchTip__Environment`, `Release`, `ServiceName`, `EnableErrors`, `EnableLogs`, `EnableTracing`, and `TracesSampleRate` for the .NET clients. This overload does not emit a separate `{RESOURCE}_URI` environment variable.

Errors are enabled by default. Logs and tracing are independently disabled by default; enabling them supplies SDK settings without adding application instrumentation. The client integrations preserve an existing OpenTelemetry pipeline and exporters. They default to PII collection and session tracking disabled, with a bounded shutdown flush.

## Additional documentation

- [Runnable example](../../examples/glitchtip/README.md)
- [Generic .NET client](../CommunityToolkit.Aspire.GlitchTip/README.md)
- [ASP.NET Core client](../CommunityToolkit.Aspire.GlitchTip.AspNetCore/README.md)
- [GlitchTip documentation](https://glitchtip.com/documentation/)
- [Aspire documentation](https://aspire.dev/)

## Feedback & contributing

Report issues and contribute through the [Aspire Community Toolkit repository](https://github.com/CommunityToolkit/Aspire). See the [contribution guide](../../CONTRIBUTING.md).
