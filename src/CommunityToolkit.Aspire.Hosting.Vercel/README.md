# Vercel hosting integration

Use this integration to model, configure, and deploy Dockerfile-based Aspire resources to Vercel.

## Getting started

Install the package in your AppHost project:

```bash
aspire add CommunityToolkit.Aspire.Hosting.Vercel
```

## Usage example

Add a `Dockerfile.vercel` file to the project you want Vercel to build, then publish it to a Vercel environment:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var vercel = builder.AddVercelEnvironment("vercel");

builder.AddProject<Projects.ApiService>("api")
       .PublishAsVercel(vercel);

builder.Build().Run();
```

By default, `aspire deploy` runs:

```bash
vercel --cwd <project-root> deploy --yes
```

Use `WithVercelProductionDeployments` to add `--prod`, `WithVercelTarget` to add `--target`, and `WithVercelScope` to deploy to a team or account scope.

Non-secret Aspire environment variables configured on the resource are processed during publish/deploy and passed to Vercel as CLI environment variables:

```csharp
builder.AddProject<Projects.ApiService>("api")
       .WithEnvironment("GREETING", "hello")
       .PublishAsVercel(vercel);
```

```bash
vercel --cwd <project-root> deploy --yes --env GREETING=hello
```

## Deployment behavior

- `aspire start` is unchanged. The Vercel environment and `PublishAsVercel` configuration are publish/deploy-only.
- `aspire publish` writes a `vercel-deployments.json` plan for the targeted resources, including the environment variable names passed to Vercel.
- `aspire deploy` validates the Vercel CLI, validates authentication, checks that each source root contains the configured Dockerfile, and invokes `vercel deploy`.
- `aspire destroy` removes the Vercel projects recorded during deployment. The Aspire CLI destroy confirmation applies before the project removal step runs.

## Prerequisites

- A `Dockerfile.vercel` file in each targeted source root. Vercel expects the container to listen on `$PORT`; the default port is `80`.
- Vercel CLI installed and on `PATH`, or configured with `WithVercelCliPath`.
- Vercel authentication from an existing CLI login or the `VERCEL_TOKEN` environment variable.
- Any project linking, secrets, domains, deployment protection, or build-time environment variables configured in Vercel.

## Known limitations

This integration does not manage Vercel secrets, provision marketplace resources, or translate Aspire service discovery references.

`aspire destroy` deletes the Vercel projects for resources targeted by this integration. Use a dedicated Vercel project for each Aspire resource, or do not run `aspire destroy` if the project contains deployments that are managed outside the Aspire AppHost.

Secret Aspire environment variables, connection strings, Docker build arguments, and Docker build secrets are rejected. Configure those values in Vercel project settings instead, because Vercel builds `Dockerfile.vercel` itself and Vercel CLI `--env` would put secret values on the command line.

Aspire command-line arguments and container entrypoint overrides are also rejected. Put runtime arguments or entrypoint behavior in `Dockerfile.vercel` instead.

## Additional documentation

- [Run any Dockerfile on Vercel](https://vercel.com/blog/dockerfile-on-vercel)
- [Vercel Container Images](https://vercel.com/docs/functions/container-images)
- [Vercel CLI deploy](https://vercel.com/docs/cli/deploy)

## Feedback & contributing

This integration is part of the .NET Aspire Community Toolkit. File issues and contribute at <https://github.com/CommunityToolkit/Aspire>.
