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
vercel --cwd <staged-source-root> deploy --yes
```

Use `WithVercelProductionDeployments` to add `--prod`, `WithVercelTarget` to add `--target`, and `WithVercelScope` to deploy to a team or account scope.

When an AppHost contains multiple compute environments, use Aspire's standard `WithComputeEnvironment` API to choose Vercel for a workload:

```csharp
var vercel = builder.AddVercelEnvironment("vercel");

builder.AddNodeApp("api", "../api", "server.mjs")
       .WithComputeEnvironment(vercel);
```

For low-level container resources, Vercel requires existing Aspire Dockerfile build metadata. Prefer the workload-specific `Add*App` integration when one exists. `WithDockerfile`, `WithDockerfileFactory`, and `WithDockerfileBuilder` are supported as advanced escape hatches. The integration always stages the source root into Aspire's deploy-time temp directory, materializes the Vercel-facing `Dockerfile` there, and runs `vercel deploy` from the staged directory so the Vercel CLI does not write `.vercel` metadata into the source tree. Staging skips `.git`, `node_modules`, and unmanaged `.vercel` metadata; linked projects keep only `.vercel/project.json`.

Non-secret Aspire environment variables configured on the resource are processed during publish/deploy and passed to Vercel as CLI environment variables:

```csharp
builder.AddNodeApp("api", "../api", "server.mjs")
       .WithEnvironment("GREETING", "hello");
```

```bash
vercel --cwd <staged-source-root> deploy --yes --env GREETING=hello
```

## Deployment behavior

- `aspire start` is unchanged. The Vercel environment is publish/deploy-only.
- `aspire publish` writes a deterministic `vercel-deployments.json` plan for the targeted resources, including the environment variable names passed to Vercel. It does not require a container registry.
- `aspire deploy` validates the Vercel CLI, validates authentication and configured scope, stages the source root, materializes the Dockerfile, invokes `vercel deploy`, and verifies the resulting deployment with `vercel inspect --wait --format=json` before saving deployment state. Vercel performs the remote source upload/build; Aspire does not build or push container images for this environment. The deployment summary and state record the deployment URL, and production deployments also record the deterministic `https://<project>.vercel.app` production URL.
- `aspire destroy` reads Aspire deployment state and removes only Vercel projects that were created by this integration for the same environment/scope. Missing state is treated as a no-op, and state is preserved if project removal fails.

## Prerequisites

- An Aspire workload with Dockerfile publish metadata, such as a language app resource that emits Dockerfile metadata or a project configured with `PublishAsDockerFile`.
- The deployed container should listen on `$PORT`; the default port is `80`.
- Vercel CLI installed and available on `PATH`.
- Vercel authentication from an existing CLI login or the `VERCEL_TOKEN` environment variable.
- Any project linking, secrets, domains, deployment protection, or build-time environment variables configured in Vercel.

Vercel supports project environment variables through `vercel env add/update`, including sensitive production and preview variables supplied on stdin. This preview integration does not mutate project environment settings yet because those values are project-scoped and require explicit ownership/update semantics. Secret Aspire values are therefore rejected instead of being passed through `vercel deploy --env`.

## Known limitations

This preview integration intentionally supports a narrow Vercel Dockerfile deployment contract:

| Aspire concept | Preview behavior |
| --- | --- |
| Dockerfile-backed compute resources | Supported through existing Aspire Dockerfile metadata and Vercel remote builds. |
| Container registries, image build, image push | Not used. Vercel uploads and builds the staged source tree. |
| Non-secret environment variables | Passed with `vercel deploy --env KEY=value`; names must use letters, digits, and underscores and values are redacted from publish output. |
| Secret parameters, connection strings, Docker build args/secrets | Rejected, including composite values that contain secrets. Configure them in Vercel project environment variables or Vercel secrets. |
| Service discovery, endpoint references, `WithReference` to another resource | Rejected because preview deployments only expose post-deploy output URLs and do not provide stable pre-deploy endpoint expressions. |
| Endpoints | HTTP and HTTPS endpoints are accepted when they map to one target port. Non-HTTP(S) endpoints or multiple target ports are rejected. |
| Volumes, bind mounts, Aspire container files | Rejected. Include required files in the source tree or use a Vercel-supported external service for state. |
| Health checks, probes, replicas, wait/dependency ordering | Rejected until they are mapped to Vercel-native behavior. |
| Container entrypoint overrides and Aspire command-line args | Rejected. Configure runtime command behavior through the Dockerfile/workload publish output or Vercel project settings. |
| Existing linked Vercel projects | Supported for deploy. Destroy preserves projects that were linked before deploy and only deletes Aspire-created projects recorded in state. |

Managed Vercel project names are inferred from the source directory name when no `.vercel/project.json` link exists. The integration slugifies the inferred name to Vercel's lowercase, hyphenated project-name form before staging and deployment. This preview intentionally does not expose resource-level project naming, alias, framework/output, or per-resource target APIs; link a Vercel project before deploy when you need an exact existing project, and add future per-resource settings through resource-specific annotations rather than overloading the environment.

## Additional documentation

- [Run any Dockerfile on Vercel](https://vercel.com/blog/dockerfile-on-vercel)
- [Vercel Container Images](https://vercel.com/docs/functions/container-images)
- [Vercel CLI deploy](https://vercel.com/docs/cli/deploy)

## Feedback & contributing

This integration is part of the .NET Aspire Community Toolkit. File issues and contribute at <https://github.com/CommunityToolkit/Aspire>.
