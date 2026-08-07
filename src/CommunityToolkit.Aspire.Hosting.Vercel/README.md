# Vercel hosting integration

Use this integration to model, configure, and deploy Aspire workloads to Vercel's Dockerfile-based hosting and Vercel Services.

## Getting started

Install the package in your AppHost project:

```bash
aspire add CommunityToolkit.Aspire.Hosting.Vercel
```

## Usage example

Add a Vercel environment and a workload that participates in Aspire's image build/push model. Language integrations such as `AddNodeApp` can emit generated Dockerfile metadata for deploy. When Vercel is the only compute environment, image-build workloads target it by default:

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

Checked-in Dockerfiles also work for low-level container resources:

```csharp
builder.AddContainer("api", "api")
       .WithDockerfile("../api", "Dockerfile");
```

By default, each public Aspire workload becomes a Vercel project root. Referenced non-public workloads become private Vercel Services inside that project and get their own VCR repository:

```bash
vercel link --cwd <scratch-project-link> --yes --project <project>
vercel pull --cwd <scratch-project-link> --yes --environment <target>
docker login vcr.vercel.com --username <owner-id> --password-stdin
aspire build/push <resource> -> vcr.vercel.com/<owner>/<project>/<service>:<tag>
docker buildx imagetools inspect --format '{{json .Manifest}}' vcr.vercel.com/<owner>/<project>/<service>:<tag>
vercel deploy --cwd <generated-build-output> --project <project> --prebuilt --yes
```

Use `WithVercelProductionDeployments` to add `--prod`, `WithVercelTarget` to add `--target`, and `WithVercelScope` to deploy to a team or account scope.

When an AppHost contains multiple compute environments, use Aspire's standard `WithComputeEnvironment` API to choose Vercel for a workload:

```csharp
var vercel = builder.AddVercelEnvironment("vercel");

builder.AddContainer("api", "api")
       .WithDockerfile("../api", "Dockerfile")
       .WithComputeEnvironment(vercel);
```

Use `WithVercelProjectName` when an Aspire-managed public project root should deploy to a specific Vercel project name instead of inferring one from the source directory:

```csharp
builder.AddContainer("api", "api")
       .WithDockerfile("../api", "Dockerfile")
       .WithVercelProjectName("my-api");
```

Linked projects with `.vercel/project.json` keep their existing provider project identity and take precedence over `WithVercelProjectName`. Project names apply only to public project roots; private services use the normalized Aspire resource name as their Vercel service and VCR repository name.

## Project and service mapping

The integration maps Aspire resources to Vercel resources using the public routing shape of your app:

| Aspire model | Vercel model |
| --- | --- |
| One Vercel-targeted resource | One Vercel project with one public root service, preserving the original single-resource behavior. |
| Multiple Vercel-targeted resources with external HTTP endpoints | One Vercel project per external HTTP resource. Each project receives public traffic through its root service. |
| A non-public resource referenced by exactly one public root | A private Vercel Service inside that public root's project. The service gets its own image repository at `vcr.vercel.com/<owner>/<project>/<service>:<tag>`. |
| A non-public resource referenced by multiple public roots | Rejected as ambiguous until an explicit mapping API exists. Deploy the shared resource separately or reference it from exactly one public workload. |
| A non-public resource with no public owner | Rejected. Add an external HTTP endpoint, reference it from one public workload, or deploy it separately. |

Vercel Services are private by default. Public traffic reaches only the project root through the generated project-level rewrite. Same-project server-to-server references are emitted as Vercel service bindings, so the caller receives a private service URL in the requested environment variable. Cross-project references are emitted as deterministic production URLs and therefore require `WithVercelProductionDeployments`.

Example with one public app and one private backend:

```csharp
var vercel = builder.AddVercelEnvironment("vercel")
    .WithVercelProductionDeployments();

var backend = builder.AddNodeApp("backend", "../backend", "server.mjs")
    .WithEndpoint(name: "http", scheme: "http", env: "PORT", isExternal: false)
    .WithComputeEnvironment(vercel);

builder.AddNodeApp("frontend", "../frontend", "server.mjs")
    .WithEndpoint(name: "http", scheme: "http", env: "PORT", isExternal: true)
    .WithEnvironment("BACKEND_URL", backend.GetEndpoint("http"))
    .WithComputeEnvironment(vercel);
```

This produces one Vercel project rooted by `frontend`, two Vercel Services (`frontend` and `backend`), two VCR repositories under the same project, and a service binding that injects `BACKEND_URL` into `frontend`.

For low-level container resources, Vercel requires Aspire image-build metadata. `WithDockerfile` is supported for checked-in Dockerfiles and Containerfiles, including custom names or paths outside the source root. Generated Dockerfiles (`WithDockerfileFactory` / `WithDockerfileBuilder`) and language integrations such as `AddNodeApp` use Aspire's built-in image build path without staging source or writing generated files into the user checkout. Resource-level `WithContainerRegistry` remains unsupported for Vercel-targeted resources because the integration must push to the Vercel project-owned VCR repository used by the generated Build Output API metadata. Add a `.dockerignore` file to keep local files such as `.env`, test artifacts, large assets, and other files out of the local Docker build context.

## How it works end to end

`aspire publish` and `aspire deploy` intentionally do different work:

1. `aspire publish` validates the targeted resources and writes `vercel-deployments.json`, a deterministic review plan with command shape, Dockerfile/source information, and environment variable names. It does not call Vercel, resolve secrets, or push images.
2. The deploy prerequisite step validates Vercel CLI, Docker buildx, Vercel auth, configured scope, existing state compatibility, Vercel project names, endpoints, and unsupported Aspire concepts before provider mutation.
3. The deploy prerequisite step groups resources into Vercel project roots and private services before provider mutation. Grouping failures happen before any Vercel project is created.
4. For each project group, the integration creates or links one Vercel project in an Aspire-owned scratch directory, configures sensitive project environment variables with `vercel env add --sensitive` over standard input, and runs [`vercel pull`](https://vercel.com/docs/cli/pull) once in that scratch directory to obtain `.vercel/project.json`, `.vercel/.env.<target>.local`, and the short-lived `VERCEL_OIDC_TOKEN`.
5. The integration decodes only routing claims from the Vercel OIDC JWT payload, configures [Vercel Container Registry](https://vercel.com/docs/container-registry) (`vcr.vercel.com/<owner>/<project>`) as each service resource's Aspire deployment target registry, and lets Aspire's built-in build/push steps build each service image and push it to the service-specific VCR repository.
6. After the push, deploy logs in to VCR again for each service, runs `docker buildx imagetools inspect --format '{{json .Manifest}}'`, and selects the linux/amd64 manifest digest. Live Vercel validation rejected OCI index digests, so the generated artifact references the concrete platform manifest accepted by [Vercel Container Images](https://vercel.com/docs/functions/container-images).
7. Deploy writes [Vercel Build Output API](https://vercel.com/docs/build-output-api) v3 files under Aspire temp/output storage: `.vercel/project.json`, root `.vercel/output/config.json` with Vercel Services and bindings, and per-service `.vercel/output/services/<service>/functions/index.func/.vc-config.json` files with `runtime: "container"` and digest-pinned VCR `handler` values.
8. Deploy runs one [`vercel deploy --prebuilt`](https://vercel.com/docs/cli/deploy) per project group, parses the deployment URL from JSON or plain CLI output, verifies readiness with [`vercel inspect --wait`](https://vercel.com/docs/cli/inspect), records deployment URLs/image digests/state, and adds deployment URLs to the pipeline summary.
9. `aspire destroy` reads saved deployment state first, removes tracked project environment variables from linked projects, deletes only Aspire-managed Vercel projects, treats already-missing projects as converged, and saves partial state after each successful delete so retry remains safe.

Non-secret Aspire environment variables configured on a resource are processed during publish/deploy and written to that service's generated Build Output container function config:

```csharp
builder.AddContainer("api", "api")
       .WithDockerfile("../api", "Dockerfile")
       .WithEnvironment("GREETING", "hello");
```

The value is not passed with `vercel deploy --env`; it is written into the generated `.vc-config.json` for that service.

Secret parameters, connection strings, and composite values that contain secrets are configured as [sensitive Vercel project environment variables](https://vercel.com/docs/environment-variables/sensitive-environment-variables) before deploy. Because [`vercel env add`](https://vercel.com/docs/cli/env) is scoped through [`vercel link`](https://vercel.com/docs/cli/link) metadata and does not accept `--project`, the integration links a temporary scratch directory outside the source tree and sends the value through standard input instead of putting it on the command line:

```csharp
var apiKey = builder.AddParameter("api-key", secret: true);

builder.AddContainer("api", "api")
       .WithDockerfile("../api", "Dockerfile")
       .WithEnvironment("API_KEY", apiKey);
```

```bash
vercel link --cwd <scratch-project-link> --yes --project <project>
vercel env add API_KEY production --cwd <scratch-project-link> --yes --force --sensitive
```

Secret project environment variables are written to `production` when `WithVercelProductionDeployments` is used, to the configured `WithVercelTarget` value when one is set, and to `preview` otherwise. Deploy treats secret environment variable names configured in the AppHost as Aspire-owned for that Vercel target and overwrites those values on each deploy.

Endpoint references to other Vercel-targeted workloads are mapped based on project grouping. Same-project references become service bindings; cross-project references become deterministic production project URLs.

```csharp
var vercel = builder.AddVercelEnvironment("vercel")
    .WithVercelProductionDeployments();

var backend = builder.AddNodeApp("backend", "../backend", "server.mjs")
    .WithEndpoint(name: "http", scheme: "http", env: "PORT", isExternal: true)
    .WithComputeEnvironment(vercel);

builder.AddNodeApp("api", "../api", "server.mjs")
    .WithEndpoint(name: "http", scheme: "http", env: "PORT", isExternal: true)
    .WithEnvironment("BACKEND_URL", backend.GetEndpoint("http"))
    .WithComputeEnvironment(vercel);
```

Because both resources are public project roots, `BACKEND_URL` is deployed as `https://<backend-project>.vercel.app`. Preview and custom-target deployments reject cross-project endpoint references because those URLs are assigned by Vercel after deployment. Same-project service bindings can only inject direct URL-valued environment variables.

## Deployment behavior

- `aspire start` is unchanged. The Vercel environment is publish/deploy-only.
- `aspire publish` writes a deterministic `vercel-deployments.json` plan for the targeted resources, including environment variable and service binding names.
- `aspire deploy` validates the Vercel CLI, Docker authentication, and configured scope; groups public project roots and private services; creates Aspire-managed Vercel projects when needed; links one temporary scratch directory per project group for project-scoped Vercel CLI operations; configures secret project environment variables; pulls Vercel project settings into that scratch directory for VCR OIDC authentication; configures one VCR repository per service for Aspire's built-in build/push steps; resolves pushed image tags to digests; generates a Vercel Services Build Output API directory; invokes `vercel deploy --prebuilt` once per project group; and verifies each deployment with `vercel inspect --wait --format=json` before saving deployment state. The deployment summary records the deployment URL, deployment state records each service's VCR image digest and Build Output API version for diagnostics, and production deployments also record the deterministic `https://<project>.vercel.app` production URL.
- `aspire destroy` reads Aspire deployment state first and removes only Vercel projects that were created by this integration for the same environment/scope. Missing state or unmanaged-only state does not require Vercel CLI/auth. State is preserved if project removal fails.

## Validation coverage

The implementation keeps Vercel-specific behavior unit-testable behind injectable services and pure helpers. Unit tests verify pipeline step registration and direct step actions, Aspire built-in build/push ordering, project/service grouping, VCR registry annotations, linux/amd64 build target selection, project/env/destroy state transitions, generated Build Output API files for Vercel Services and service bindings, secret redaction, exact CLI argument boundaries, Docker digest parsing, Vercel deploy/inspect output parsing, compact JWT claim decoding, Vercel dotenv parsing, TypeScript AppHost publish/projection, the TypeScript start smoke when `pwsh` is available, and failure messages for unsupported Aspire concepts.

The integration was also validated against Vercel end to end during development: Dockerfile workloads were built and pushed to VCR through Aspire's built-in pipeline, deployed with `vercel deploy --prebuilt`, verified with `vercel inspect`, called through the deployed URL where deployment protection allowed it, and cleaned up with destroy. The Vercel Services Build Output shape was separately deployed against real disposable Vercel projects to confirm `vercel deploy --prebuilt` accepts generated services and bindings.

## Prerequisites

- An Aspire workload that participates in Aspire's image build/push model, such as a .NET project, a language app resource that emits Dockerfile metadata, or a resource configured with `WithDockerfile`.
- The deployed container should listen on `$PORT`; the default port is `80`.
- Vercel CLI 54.18.6 or later installed and available on `PATH`. This preview depends on `vercel link`, `vercel pull`, `deploy --prebuilt`, `inspect --wait --timeout --format=json`, `env add --sensitive`, and `project remove`; older CLI versions are rejected during deploy preflight.
- Docker CLI with buildx and a running Docker daemon for Aspire's built-in image build/push and VCR digest inspection.
- Vercel authentication from an existing CLI login or the `VERCEL_TOKEN` environment variable.
- Vercel project access that allows `vercel pull` to mint the `VERCEL_OIDC_TOKEN` used by VCR.
- Any domains, deployment protection, or build-time environment variables configured in Vercel.

## Known limitations

This preview integration intentionally supports a narrow Vercel Dockerfile deployment contract:

| Aspire concept | Preview behavior |
| --- | --- |
| Image-build compute resources | Supported through Aspire's built-in project/Dockerfile image build and push pipeline, Vercel Container Registry, digest resolution, and Build Output API prebuilt deploy. .NET projects, checked-in custom Dockerfile names, non-root Dockerfile paths, and generated Dockerfiles from language app integrations are supported. |
| Container registries, image build, image push | Uses Vercel Container Registry and the Vercel OIDC token from `vercel pull` as the deployment target registry for Aspire's built-in push steps. Resource-level `WithContainerRegistry` is rejected for Vercel-targeted resources. |
| Public routing and project/service cardinality | A single resource deploys as one project for compatibility. With multiple resources, external HTTP workloads are public Vercel project roots; referenced non-public workloads become private Vercel Services inside one owning project. Shared private services referenced by multiple public roots are rejected. |
| Non-secret environment variables | Written into the generated per-service Build Output function config; names must use letters, digits, and underscores and values are redacted from publish output. |
| Secret parameters and connection strings | Configured as sensitive Vercel project environment variables with `vercel env add --force --sensitive`; values are supplied on stdin and redacted from publish output. Missing secret values are rejected. |
| Docker build args/secrets | Supported when the underlying Aspire image build integration supports them; Aspire's built-in build step handles the local Docker build before Vercel deploy receives only Build Output API metadata. |
| Service discovery, endpoint references, `WithReference` to another resource | Same-project direct URL references become Vercel service bindings. Cross-project references are supported for external HTTP(S) endpoints on workloads in the same Vercel production environment by using deterministic `https://<project>.vercel.app` URLs. Cross-project references are rejected for preview/custom targets because those URLs are assigned after deployment. |
| Endpoints | External HTTP and HTTPS endpoints with HTTP transports are accepted as public project roots when they map to one target port. Internal HTTP endpoints are accepted only for services owned by one public root or for single-resource compatibility. Non-HTTP(S) endpoints/transports or multiple target ports are rejected. |
| Volumes, bind mounts, Aspire container files | Rejected. Include required files in the source tree or use a Vercel-supported external service for state. |
| Health checks, replicas, wait/dependency ordering | Ignored by deployment planning until they are mapped to Vercel-native behavior. Probes are rejected. |
| Container entrypoint overrides and Aspire command-line args | Rejected. Configure runtime command behavior through the Dockerfile/workload publish output or Vercel project settings. |
| Existing linked Vercel projects | Supported for deploy. Destroy preserves projects that were linked before deploy and only deletes Aspire-created projects recorded in state. |

Managed Vercel project names are inferred from the public root source directory name when no `.vercel/project.json` link exists and no explicit `WithVercelProjectName` is configured. The integration slugifies inferred names to Vercel's lowercase, hyphenated project-name form before deployment and rejects duplicate public project names within the same Vercel environment, including linked projects and explicitly configured names. Service names are inferred from Aspire resource names and must be unique within the Vercel environment. This preview intentionally does not yet expose resource-level alias, domain, framework/output, build-setting, deployment-protection, shared-private-service, or per-resource target APIs; link public roots to existing Vercel projects before deploy when you need existing provider project identities.

If the source root already contains a `vercel.json`, the integration reads it only for validation; the source file is not modified. Top-level Vercel build, env, routing, function, and services settings are rejected because this preview owns the generated Build Output API container function, catch-all route, and AppHost-driven environment variables.

## Additional documentation

- [Run any Dockerfile on Vercel](https://vercel.com/blog/dockerfile-on-vercel)
- [Vercel Container Images](https://vercel.com/docs/functions/container-images)
- [Vercel Container Registry](https://vercel.com/docs/container-registry)
- [Vercel Build Output API](https://vercel.com/docs/build-output-api)
- [Build Output API configuration](https://vercel.com/docs/build-output-api/configuration)
- [Vercel Services](https://vercel.com/docs/vercel-platform/services)
- [Vercel Service bindings](https://vercel.com/docs/vercel-platform/services/service-bindings)
- [Vercel CLI deploy](https://vercel.com/docs/cli/deploy)
- [Vercel CLI link](https://vercel.com/docs/cli/link)
- [Vercel CLI pull](https://vercel.com/docs/cli/pull)
- [Vercel CLI env](https://vercel.com/docs/cli/env)
- [Vercel CLI inspect](https://vercel.com/docs/cli/inspect)
- [Sensitive environment variables](https://vercel.com/docs/environment-variables/sensitive-environment-variables)

## Feedback & contributing

This integration is part of the .NET Aspire Community Toolkit. File issues and contribute at <https://github.com/CommunityToolkit/Aspire>.
