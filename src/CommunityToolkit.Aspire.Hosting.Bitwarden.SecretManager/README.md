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
- `WithStateFile(...)` overrides the local deployment state JSON file location.
- `WithAuthStateFile(...)` overrides the local Bitwarden SDK auth state file location.
- `WithRuntimeAccessToken(...)` overrides the token injected into dependents.

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

During `aspire deploy`, the integration runs a Bitwarden deployment step that:

- resolves declared project and secret configuration
- connects to Bitwarden using configured credentials
- creates or updates the project
- creates or updates managed secrets

This keeps the experience declaration-first: resources and references are your contract, and deployment materializes that contract.

In day-to-day usage, you can treat Bitwarden API orchestration as an internal detail of the integration.
