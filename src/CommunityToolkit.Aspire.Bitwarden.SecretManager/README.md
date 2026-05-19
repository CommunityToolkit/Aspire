# CommunityToolkit.Aspire.Bitwarden.SecretManager

## Overview

`CommunityToolkit.Aspire.Bitwarden.SecretManager` registers authenticated `BitwardenClient` instances using structured Aspire configuration.

## Installation

```bash
dotnet add package CommunityToolkit.Aspire.Bitwarden.SecretManager
```

## Configuration

The client integration expects configuration under `Aspire:Bitwarden:SecretManager:{connectionName}`.

When used with the hosting integration, call `WithReference(bitwarden)` in the AppHost and then register the client in the consuming application:

```csharp
builder.AddBitwardenSecretManagerClient("bitwarden");
```

The configuration section includes:

- `OrganizationId`
- `ProjectId`
- `AccessToken`
- `ApiUrl`
- `IdentityUrl`

## Usage

```csharp
builder.AddBitwardenSecretManagerClient("bitwarden");

WebApplication app = builder.Build();

app.MapGet("/sync", (Bitwarden.Sdk.BitwardenClient client, BitwardenSecretManagerClientSettings settings) =>
{
    var sync = client.Secrets.Sync(settings.OrganizationId, null);
    return Results.Ok(sync.Data.Count);
});
```

Use `AddKeyedBitwardenSecretManagerClient(...)` when you need multiple Bitwarden clients in the same application.