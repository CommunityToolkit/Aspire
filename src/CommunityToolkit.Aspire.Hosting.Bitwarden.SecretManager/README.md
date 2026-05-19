# CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager

## Overview

`CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager` adds a non-container Aspire hosting resource for Bitwarden Secrets Manager projects and secrets.

The resource reconciles a Bitwarden project during AppHost startup, can manage named secrets inside that project, and exposes structured metadata to dependent applications through `WithReference(...)`.

## Installation

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Bitwarden.SecretManager
```

## Configuration

Create parameters for the Bitwarden project name, organization identifier, and access token, then add the Bitwarden resource to your AppHost. The Aspire resource name and the Bitwarden project name are independent.

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

Optional configuration:

- `WithExistingProject(...)` adopts an existing Bitwarden project by identifier.
- `WithApiUrl(...)` and `WithIdentityUrl(...)` override the Bitwarden endpoints.
- `WithStateFile(...)` overrides the Bitwarden SDK state file location.
- `WithRuntimeAccessToken(...)` overrides the token injected into dependents.

## Usage

Use `AddSecret(...)` to manage remote Bitwarden secrets during startup.

```csharp
IResourceBuilder<ParameterResource> apiKey = builder.AddParameter("api-key", secret: true);

IResourceBuilder<BitwardenSecretResource> managedSecret = bitwarden.AddSecret("api-key", apiKey);
```

Use `GetSecret(...)` to reference existing remote secrets.

```csharp
IBitwardenSecretReference existingSecret = bitwarden.GetSecret("shared-api-key");
```

Use `WithReference(...)` to inject structured Bitwarden client configuration into dependent resources.

```csharp
builder.AddProject<Projects.ApiService>("api")
    .WithReference(bitwarden);
```

The injected configuration is available under `Aspire:Bitwarden:SecretManager:{connectionName}` and includes:

- `OrganizationId`
- `ProjectId`
- `AccessToken`
- `ApiUrl`
- `IdentityUrl`