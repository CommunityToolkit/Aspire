# CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager

## Overview

`CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager` helps you work with Bitwarden Secrets Manager in your Aspire AppHost.

Use it to define your Bitwarden project and secrets in one place, then apply them with `aspire deploy`.

## Getting Started

### Install the package

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager
```

### Basic setup

Create parameters for the project name, organization ID, and access token, then add the Bitwarden resource to your AppHost. The Aspire resource name and the Bitwarden project name are independent.

```csharp
IResourceBuilder<ParameterResource> organizationId = builder.AddParameter("bitwarden-organization-id");
IResourceBuilder<ParameterResource> accessToken = builder.AddParameter("bitwarden-access-token", secret: true);
IResourceBuilder<ParameterResource> projectName = builder.AddParameter("bitwarden-project-name");

IResourceBuilder<BitwardenSecretManagerResource> bitwarden = builder.AddBitwardenSecretManager(
    "bitwarden",
    projectName,
    organizationId,
    accessToken);
```

### Optional configuration

You can further customize the resource with the following options:

- `WithExistingProject(...)` adopts an existing Bitwarden project by identifier.
- `WithApiUrl(...)` and `WithIdentityUrl(...)` override the Bitwarden endpoints.
- `WithCacheFile(...)` overrides the AppHost cache file location (default: `.bitwarden/{resourceName}.{environment}.json` relative to the AppHost directory). The AppHost cache tracks Bitwarden project and secret IDs between runs. Relative paths are resolved from the AppHost directory.
- `WithAuthCacheFile(...)` overrides the AppHost auth cache file location (default: Aspire store, keyed by a hash of the access token). The AppHost auth cache persists the Bitwarden SDK auth session between runs on the AppHost. Relative paths are resolved from the Aspire store.

Pass a least-privilege read-only access token directly to `WithReference(...)` so the client does not receive the management token:

```csharp
IResourceBuilder<ParameterResource> runtimeToken = builder.AddParameter("runtime-access-token", secret: true);

builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden, runtimeToken);
```

> **Note:** The client token must be granted read permissions to the Bitwarden project. Bitwarden does not expose an API for granting project access to a service account, so this step cannot be automated. You must grant the service account read access to the project manually in the Bitwarden web vault or CLI. For a newly created project, do this after the first AppHost run that creates the project.


Use `WithAuthCacheFile(...)` on a dependent resource builder to persist its Bitwarden SDK auth session across restarts. Accepts a string for a fixed path or a parameter for an environment-specific path:

```csharp
builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden)
    .WithAuthCacheFile(bitwarden, "/data/bitwarden/auth-cache");                   // fixed path
    // or:
    .WithAuthCacheFile(bitwarden, builder.AddParameter("auth-cache-location"));    // env-specific path
```

## Usage

Use `AddSecret(...)` to declare managed Bitwarden secrets.

```csharp
IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("api-key", secret: true);

IResourceBuilder<BitwardenSecretResource> managedSecret = bitwarden.AddSecret("api-key", apiKey);
```

Use `GetSecret(...)` to reference an existing remote secret.

```csharp
IBitwardenSecretReference existingSecret = bitwarden.GetSecret("shared-api-key");
```

Use `WithReference(...)` to inject Bitwarden client configuration into dependent resources.

```csharp
builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden);
```

Use `WithBitwardenSecretValue(...)` and `WithBitwardenSecretId(...)` to pass managed or referenced secrets to dependent resources.

```csharp
IResourceBuilder<BitwardenSecretResource> managedSecret = bitwarden.AddSecret("demo-api-key", apiKey);

builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden)
    .WithBitwardenSecretValue("DEMO_API_KEY", managedSecret.Resource)
    .WithBitwardenSecretId("DEMO_API_KEY_SECRET_ID", managedSecret.Resource);
```

The injected configuration is available under `Aspire:Bitwarden:SecretManager:{connectionName}` and includes:

- `OrganizationId`
- `ProjectId`
- `AccessToken`
- `ApiUrl`
- `IdentityUrl`

## Deployment

Deployment applies your declared Bitwarden resources.

Typical flow:

1. Declare the Bitwarden project and any managed secrets in the AppHost graph.
2. Run `aspire deploy` for the AppHost.

During `aspire deploy`, the integration runs four pipeline steps per Bitwarden resource:

1. **Authenticate** — resolves credentials and authenticates with Bitwarden Secrets Manager.
2. **Provision project** — creates or updates the remote Bitwarden project.
3. **Provision secrets** — creates or updates managed secrets and validates declared references.
4. **Patch env files** — applies resolved values to Docker Compose environment files (Docker Compose deployments only).

This keeps the experience declaration-first: resources and references are your contract, and deployment materializes that contract.

