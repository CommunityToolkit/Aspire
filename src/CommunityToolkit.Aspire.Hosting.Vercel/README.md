# Vercel hosting integration

Use this integration to model, configure, and deploy Aspire workloads to Vercel's Dockerfile-based hosting.

## Getting started

Install the package in your AppHost project:

```bash
aspire add CommunityToolkit.Aspire.Hosting.Vercel
```

## Usage example

Add a Vercel environment and a workload with Aspire Dockerfile publish metadata. When Vercel is the only compute environment, Dockerfile-backed workloads target it by default:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddVercelEnvironment("vercel");

builder.AddNodeApp("api", "../api", "server.mjs");

builder.Build().Run();
```

.NET projects use Aspire's existing Dockerfile publish support:

```csharp
builder.AddProject<Projects.ApiService>("api")
       .PublishAsDockerFile();
```

By default, `aspire deploy` runs:

```bash
vercel --cwd <staged-source-root> deploy --yes --project <project>
```

Use `WithVercelProductionDeployments` to add `--prod`, `WithVercelTarget` to add `--target`, and `WithVercelScope` to deploy to a team or account scope.

When an AppHost contains multiple compute environments, use Aspire's standard `WithComputeEnvironment` API to choose Vercel for a workload:

```csharp
var vercel = builder.AddVercelEnvironment("vercel");

builder.AddNodeApp("api", "../api", "server.mjs")
       .WithComputeEnvironment(vercel);
```

Use `WithVercelProjectName` when an Aspire-managed resource should deploy to a specific Vercel project name instead of inferring one from the source directory:

```csharp
builder.AddNodeApp("api", "../api", "server.mjs")
       .WithVercelProjectName("my-api");
```

Linked projects with `.vercel/project.json` keep their existing provider project identity and take precedence over `WithVercelProjectName`.

For low-level container resources, Vercel requires existing Aspire Dockerfile build metadata. Prefer the workload-specific `Add*App` integration when one exists. `WithDockerfile`, `WithDockerfileFactory`, and `WithDockerfileBuilder` are supported as advanced escape hatches. The integration always stages the source root into Aspire's deploy-time temp directory, materializes the Vercel-facing `Dockerfile.vercel` there, writes a services-mode `vercel.json` that routes all traffic to a generated `app` container service, and runs `vercel deploy` from the staged directory so the Vercel CLI does not write `.vercel` metadata into the source tree. Staging skips `.git`, `node_modules`, and unmanaged `.vercel` metadata; linked projects keep only `.vercel/project.json`. Source roots containing symbolic links or reparse points are rejected because staging would otherwise change symlink semantics or copy data from outside the source root. Add a `.vercelignore` file to exclude local files such as `.env`, test artifacts, large assets, and other files that should not be uploaded to Vercel. Deploy emits a warning when root `.env*` files are present and are not covered by `.vercelignore`.

Non-secret Aspire environment variables configured on the resource are processed during publish/deploy and passed to Vercel as deployment-scoped CLI environment variables:

```csharp
builder.AddNodeApp("api", "../api", "server.mjs")
       .WithEnvironment("GREETING", "hello");
```

```bash
vercel --cwd <staged-source-root> deploy --yes --project <project> --env GREETING=hello
```

Secret parameters, connection strings, and composite values that contain secrets are configured as sensitive Vercel project environment variables before deploy. The integration links the staged project when needed, then sends the value through standard input instead of putting it on the command line:

```csharp
var apiKey = builder.AddParameter("api-key", secret: true);

builder.AddNodeApp("api", "../api", "server.mjs")
       .WithEnvironment("API_KEY", apiKey);
```

```bash
vercel --cwd <staged-source-root> env add API_KEY production --yes --force --sensitive
```

Secret project environment variables are written to `production` when `WithVercelProductionDeployments` is used, to the configured `WithVercelTarget` value when one is set, and to `preview` otherwise. Deploy treats secret environment variable names configured in the AppHost as Aspire-owned for that Vercel target and overwrites those values on each deploy.

Production endpoint references to other Vercel-targeted workloads are converted to deterministic Vercel project URLs:

```csharp
var vercel = builder.AddVercelEnvironment("vercel")
    .WithVercelProductionDeployments();

var backend = builder.AddNodeApp("backend", "../backend", "server.mjs")
    .WithEndpoint(name: "http", scheme: "http", env: "PORT", isExternal: true)
    .WithComputeEnvironment(vercel);

builder.AddNodeApp("api", "../api", "server.mjs")
    .WithEnvironment("BACKEND_URL", backend.GetEndpoint("http"))
    .WithComputeEnvironment(vercel);
```

The `BACKEND_URL` value is deployed as `https://<backend-project>.vercel.app`. Endpoint references must target external HTTP or HTTPS endpoints on workloads in the same Vercel environment. Preview and custom-target deployments still reject endpoint references because those URLs are assigned by Vercel after deployment.

## Deployment behavior

- `aspire start` is unchanged. The Vercel environment is publish/deploy-only.
- `aspire publish` writes a deterministic `vercel-deployments.json` plan for the targeted resources, including the environment variable names passed to Vercel. It does not require a container registry.
- `aspire deploy` validates the Vercel CLI, validates authentication and configured scope, stages the source root, materializes `Dockerfile.vercel` and the required services-mode `vercel.json`, invokes `vercel deploy`, and verifies the resulting deployment with `vercel inspect --wait --format=json` before saving deployment state for each verified resource. Vercel performs the remote source upload/build; Aspire does not build or push container images for this environment. The deployment summary and state record the deployment URL, and production deployments also record the deterministic `https://<project>.vercel.app` production URL.
- `aspire destroy` reads Aspire deployment state first and removes only Vercel projects that were created by this integration for the same environment/scope. Missing state or unmanaged-only state does not require Vercel CLI/auth. State is preserved if project removal fails.

## Prerequisites

- An Aspire workload with Dockerfile publish metadata, such as a language app resource that emits Dockerfile metadata or a project configured with `PublishAsDockerFile`.
- The deployed container should listen on `$PORT`; the default port is `80`.
- Vercel CLI 54.18.6 or later installed and available on `PATH`. This preview depends on `deploy --env`, `inspect --wait --timeout --format=json`, and `project remove`; older CLI versions are rejected during deploy preflight.
- Vercel authentication from an existing CLI login or the `VERCEL_TOKEN` environment variable.
- Any domains, deployment protection, or build-time environment variables configured in Vercel.

## Known limitations

This preview integration intentionally supports a narrow Vercel Dockerfile deployment contract:

| Aspire concept | Preview behavior |
| --- | --- |
| Dockerfile-backed compute resources | Supported through existing Aspire Dockerfile metadata and Vercel remote builds. |
| Container registries, image build, image push | Not used. Vercel uploads and builds the staged source tree. |
| Non-secret environment variables | Passed with `vercel deploy --env KEY=value`; names must use letters, digits, and underscores and values are redacted from publish output. |
| Secret parameters and connection strings | Configured as sensitive Vercel project environment variables with `vercel env add --force --sensitive`; values are supplied on stdin and redacted from publish output. Missing secret values are rejected. |
| Docker build args/secrets | Rejected. Vercel runs the Dockerfile build remotely, so configure build-time values in Vercel project settings. |
| Service discovery, endpoint references, `WithReference` to another resource | Supported for external HTTP(S) endpoints on workloads in the same Vercel production environment by using deterministic `https://<project>.vercel.app` URLs. Rejected for preview/custom targets because those URLs are assigned after deployment. |
| Endpoints | External HTTP and HTTPS endpoints with HTTP transports are accepted when they map to one target port. Internal endpoints, non-HTTP(S) endpoints/transports, or multiple target ports are rejected. |
| Volumes, bind mounts, Aspire container files | Rejected. Include required files in the source tree or use a Vercel-supported external service for state. |
| Health checks, probes, replicas, wait/dependency ordering | Rejected until they are mapped to Vercel-native behavior. |
| Container entrypoint overrides and Aspire command-line args | Rejected. Configure runtime command behavior through the Dockerfile/workload publish output or Vercel project settings. |
| Existing linked Vercel projects | Supported for deploy. Destroy preserves projects that were linked before deploy and only deletes Aspire-created projects recorded in state. |

Managed Vercel project names are inferred from the source directory name when no `.vercel/project.json` link exists and no explicit `WithVercelProjectName` is configured. The integration slugifies inferred names to Vercel's lowercase, hyphenated project-name form before staging and deployment, and rejects duplicate project names within the same Vercel environment, including linked projects and explicitly configured names. Each Aspire resource must map to one distinct Vercel project because production endpoint references use the project-level `https://<project>.vercel.app` URL. This preview intentionally does not yet expose resource-level alias, domain, framework/output, build-setting, deployment-protection, or per-resource target APIs; link each resource to a distinct Vercel project before deploy when you need existing provider project identities.

If the source root already contains a `vercel.json`, the integration preserves top-level URL/routing configuration that is valid in Vercel services mode and appends the generated catch-all service rewrite. Existing `services` configuration, legacy `routes`, deprecated top-level `env`/`build`/`builds`, and top-level build/runtime settings such as `framework`, `buildCommand`, or `outputDirectory` are rejected because this preview owns the generated container service shape, catch-all service routing, and AppHost-driven environment variables.

## Additional documentation

- [Run any Dockerfile on Vercel](https://vercel.com/blog/dockerfile-on-vercel)
- [Vercel Container Images](https://vercel.com/docs/functions/container-images)
- [Vercel CLI deploy](https://vercel.com/docs/cli/deploy)

## Feedback & contributing

This integration is part of the .NET Aspire Community Toolkit. File issues and contribute at <https://github.com/CommunityToolkit/Aspire>.
