# GlitchTip example

This example connects an ASP.NET Core API to one GlitchTip project. Local execution starts GlitchTip and PostgreSQL. Deployment uses a pre-existing shared GlitchTip instance and creates or resolves the project there.

## Run locally

Use .NET 10, Aspire 13.5, and a running Docker-compatible container runtime. From this directory:

```shell
cd GlitchTip.AppHost
aspire start
```

Use `aspire start --isolated` when another worktree has an AppHost running. The project's default slug is `glitchtip-demo`; local state is isolated by the AppHost directory rather than by changing project names.

Open the `api` endpoint from the Aspire dashboard. `/health` returns a successful health response. `/error` intentionally throws an exception that the ASP.NET Core client reports to GlitchTip. `/log` emits a warning-level structured log and returns a successful response. The example enables errors, structured logs, and tracing, and adds ASP.NET Core OpenTelemetry instrumentation.

The `glitchtip-server` resource exposes links to GlitchTip and its enabled MCP endpoint. The project setup log identifies the local administrator's email and secret password parameter names. Authenticate in your MCP client before using the MCP endpoint.

Stopping Aspire preserves the local instance. A reset requires stopping this AppHost and removing its two scoped Docker volumes: PostgreSQL and uploads. Identify the exact volume names first; they contain a hash of this AppHost directory. See the [hosting README](../../src/CommunityToolkit.Aspire.Hosting.GlitchTip/README.md) for the persistence and reset contract.

## TypeScript AppHost

`GlitchTip.AppHost.TypeScript` exercises the same hosting APIs with a container consumer. From that directory, run `aspire restore`, then `aspire start --isolated`. It declares one project, an HTTP health monitor, and telemetry settings. `addGlitchTip` also supplies the default deployment parameter declarations; local execution does not prompt for their values. The Nginx consumer demonstrates hosting configuration; it does not emit Sentry telemetry itself.

Run `npm run build` to check the generated TypeScript contract, including the compile-only optional binding example in `deployment-contract.mts`. The generated Aspire modules and compiled output are not source files to edit.
### Local ingestion verification

Call `/error` and `/log` with a known W3C `traceparent` header, then inspect the project's events, the organization's logs, and transaction groups through GlitchTip's read APIs. Compare transaction counts before and after the requests. This checks persisted ingestion rather than only an SDK queue or successful HTTP submission.

In the local GlitchTip 6.2.6 sample, both requests produce stored telemetry. Errors and logs retain `service.name=api`, environment `Development`, release `local`, and the incoming trace identity; default user PII is absent. The `/log` transaction appears as `GET /log`. The unhandled `/error` request appears as `Microsoft.AspNetCore.Hosting.HttpRequestIn`, and its transaction group's error count does not increment, although the separate error event is captured. This is an observation of this sample and instrumentation configuration, not a universal SDK contract.
## Deploy to a shared instance

The C# AppHost includes a Docker Compose environment. `AddGlitchTip` automatically declares `glitchtip-url`, `glitchtip-organization`, `glitchtip-initial-team`, and the secret `glitchtip-api-token` for publish and deploy. Local execution uses its own instance without requiring those values. Set these Aspire parameters in the deployment environment:

| Parameter | Value |
| --- | --- |
| `glitchtip-project` | A stable slug unique to this stack in the target organization |
| `release` | The deployed build or image identifier |
| `glitchtip-url` | The shared GlitchTip base URL |
| `glitchtip-organization` | An existing organization slug |
| `glitchtip-initial-team` | An existing team slug, used only when creating the project |
| `glitchtip-api-token` | A secret management token with access to the declared operations |
| `api-health-url` | The complete API health URL reachable from GlitchTip, with a fully qualified hostname or IP address |

To reuse parameters already defined by your AppHost, use the optional `WithDeploymentParameters` binding shown in the [hosting README](../../src/CommunityToolkit.Aspire.Hosting.GlitchTip/README.md#publish-and-deploy). The example uses the automatic defaults.

Run `aspire deploy --environment production` from the AppHost directory with the platform's normal Compose deployment configuration. `production` becomes the reporting environment. The pipeline resolves a fresh DSN before preparing Compose and reconciles monitors before application rollout. A management failure blocks deployment. There is no DSN override or cache fallback, and no GlitchTip readiness check is added to the running API.

The sample uses `api-health-url` during deployment. Supply a complete URL including `/health`. GlitchTip 6.2.6 rejects bare Compose service names; the URL needs a fully qualified hostname or IP address that the shared server can reach. Monitor requests are unauthenticated GETs. Deployment does not probe the endpoint, invent DNS aliases, or discover reverse-proxy routes.

`aspire publish` only generates Compose assets. It does not contact GlitchTip or replace the deployment pipeline's project and DSN resolution. Shared projects, events, and artifacts are retained when the stack stops or is removed. Team membership is managed externally after initial creation.

## Extend the example

Use `WithGlitchTipSourceMaps` for prepared JavaScript/source-map output with matching debug IDs, and `WithGlitchTipDebugSymbols` for dSYM, PDB, or ELF output available on the deploy machine. The pipeline uploads explicitly registered files after the build and verifies server processing before rollout. The example does not register artifact paths because it has no JavaScript build or configured native-symbol output.

See the [hosting README](../../src/CommunityToolkit.Aspire.Hosting.GlitchTip/README.md), [generic client README](../../src/CommunityToolkit.Aspire.GlitchTip/README.md), and [ASP.NET Core client README](../../src/CommunityToolkit.Aspire.GlitchTip.AspNetCore/README.md) for the complete contracts and current limitations.
