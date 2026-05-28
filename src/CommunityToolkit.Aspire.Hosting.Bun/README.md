# CommunityToolkit.Aspire.Hosting.Bun library

> **⚠️ DEPRECATION NOTICE**  
> This package is deprecated. Bun support is now available in the core `Aspire.Hosting.JavaScript` package via `AddBunApp(...)`.
>
> **Migration Guide:**
> - Install `Aspire.Hosting.JavaScript` and remove `CommunityToolkit.Aspire.Hosting.Bun`.
> - Keep your existing `builder.AddBunApp(...)` calls, but use the core package implementation.
> - If you're using TypeScript AppHost bindings, continue using `builder.addBunApp(...)` from the generated core bindings.
>
> This package will be removed in a future release. Please migrate to `Aspire.Hosting.JavaScript`.

Provides extension methods and resource definitions for an Aspire AppHost to configure a Bun project.

## Getting Started

### Install the package

In your AppHost project, install the package using the following command:

```dotnetcli
dotnet add package CommunityToolkit.Aspire.Hosting.Bun
```

### Example usage

Then, in the _Program.cs_ file of `AppHost`, define a Bun resource, then call `AddBunApp`:

```csharp
builder.AddBunApp("bun-server", "main.ts")
    .WithHttpEndpoint(env: "PORT")
    .WithEndpoint();
```

### Migration from Toolkit Bun to core Bun

Toolkit API:

```csharp
builder.AddBunApp("api", "server.ts")
    .WithHttpEndpoint(env: "PORT");
```

Core API (`Aspire.Hosting.JavaScript`):

```csharp
builder.AddBunApp("api", "./api", "server.ts")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();
```

TypeScript AppHost (core bindings):

```typescript
await builder
    .addBunApp("api", "./api", "server.ts")
    .withHttpEndpoint({ env: "PORT" })
    .withExternalHttpEndpoints();
```

## Additional Information

https://learn.microsoft.com/dotnet/aspire/community-toolkit/hosting-bun

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
