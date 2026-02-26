# CommunityToolkit.Aspire.Hosting.Zitadel library

Provides extension methods and resource definitions for a .NET Aspire AppHost to configure Zitadel.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Zitadel
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Zitadel resource, then call `AddZitadel`:

```csharp
builder.AddZitadel("zitadel");
```

Zitadel *requires* a Postgres database, you can add one with `WithDatabase`:
```csharp
var database = builder.AddPostgres("postgres");

builder.AddZitadel("zitadel")
    .WithDatabase(database);
```
You can also pass in a database rather than server (`AddPostgres().AddDatabase()`).

### Configuring the External Domain

By default, Zitadel uses `{name}.dev.localhost` as the external domain, which works well for local development. For production deployments or custom scenarios, you can configure a custom external domain:

**Option 1: Using the parameter**
```csharp
builder.AddZitadel("zitadel", externalDomain: "auth.example.com");
```

**Option 2: Using the fluent API**
```csharp
builder.AddZitadel("zitadel")
    .WithExternalDomain("auth.example.com");
```

**Option 3: From configuration**
```csharp
var domain = builder.Configuration["Zitadel:ExternalDomain"];
builder.AddZitadel("zitadel", externalDomain: domain);
```

#### Why `.dev.localhost`?

`.dev.localhost` is a special top-level domain that:
- Automatically resolves to `127.0.0.1` without requiring DNS configuration
- Provides unique subdomains for each Zitadel instance (e.g., `zitadel1.dev.localhost`, `zitadel2.dev.localhost`)
- Works reliably in local development and CI/CD environments
- Satisfies Zitadel's requirement for stable hostnames in OIDC/OAuth2 flows

For production deployments, replace this with your actual domain name using one of the configuration methods above.

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
