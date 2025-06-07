# CommunityToolkit.Aspire.Hosting.Supabase

A hosting integration for spinning up a full Supabase stack (Postgres + API + child modules) as in-memory container resources for local development and testing.

## Installation

Add the NuGet package to your .NET application:

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Supabase
```

## Getting Started

```csharp
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Create a standalone Supabase core (Postgres + HTTP API)
var supabase = builder.AddSupabase(
    name: "supabase",
    password: null,         // null to generate a random password parameter
    apiPort: 8000,
    dbPort: 5432);

// Or include all Supabase child modules (Kong, Studio, Auth, Realtime, Storage, etc.)
builder.AddAllSupabase(
    name: "supabase",
    password: null,
    apiPort: 8000,
    dbPort: 5432);

var app = builder.Build();
app.Run();
```

When built out, this will launch:
- A Postgres container with:
  - `POSTGRES_USER=postgres`
  - `POSTGRES_DB=postgres`
  - `POSTGRES_PASSWORD` injected via a parameter resource
  - Mounted volumes under `./volumes/db/` for data, migrations, and init scripts
- A Supabase HTTP API container exposing port 8000
- (With `AddAllSupabase`) Child containers for Kong, Studio, REST, Realtime, Storage API, Auth (GoTrue), Meta, Inbucket, Image Proxy, Logflare, and Edge Runtime.

## Core API

### AddSupabase

```csharp
public static IResourceBuilder<SupabaseResource> AddSupabase(
    this IDistributedApplicationBuilder builder,
    string name,
    IResourceBuilder<ParameterResource>? password = null,
    int? apiPort = null,
    int? dbPort = null);
```

- **name**: The logical name of the Supabase resource.
- **password**: Optional `ParameterResource` builder for the Postgres password; default is a random-generated parameter.
- **apiPort**: Host port to bind the Supabase HTTP API (default 8000).
- **dbPort**: Host port to bind the Postgres database (default 5432).

This registers:
1. A `SupabaseResource` container running Postgres and HTTP API images
2. Environment variables for `POSTGRES_USER`, `POSTGRES_DB`, `POSTGRES_PASSWORD`, and standard `PG*` variables
3. Volume binds for:
   - `./volumes/db/data` → `/var/lib/postgresql/data`
   - `./volumes/db/migrations` → `/docker-entrypoint-initdb.d/migrations`
   - `./volumes/db/init-scripts` → `/docker-entrypoint-initdb.d/init-scripts`
4. Two endpoints on the resource:
   - `http`: API port
   - `postgresql`: Database port

### AddAllSupabase

```csharp
public static IResourceBuilder<SupabaseResource> AddAllSupabase(
    this IDistributedApplicationBuilder builder,
    string name,
    IResourceBuilder<ParameterResource>? password = null,
    int? apiPort = null,
    int? dbPort = null);
```

Chains `AddSupabase(...)` and then adds all child services via extension methods:
- `WithKong()`
- `WithStudio()`
- `WithRest()`
- `WithRealtime()`
- `WithStorageService()`
- `WithAuthService()`
- `WithMetaService()`
- `WithInbucketService()`
- `WithImageProxyService()`
- `WithLogflareService()`
- `WithEdgeRuntimeService()`

## Child Modules

Each child service is added with a `.WithXXX()` method. For example:

```csharp
supabase.WithAuthService();
```

These methods launch the corresponding container images with default configuration and link them to the core Supabase resource.

## Customizing Data & Migrations

Place your SQL migrations under:
```
./volumes/db/migrations
```

Any additional init scripts (roles, extensions, JWT setup) go under:
```
./volumes/db/init-scripts
```

Persistent database data is stored in:
```
./volumes/db/data
```

## SupabaseResource

The `SupabaseResource` exposes two endpoints:

- **PrimaryEndpoint** (`http`) – for HTTP API calls
- **DatabaseEndpoint** (`postgresql`) – for Postgres connections

Use the provided `ConnectionStringExpression` to wire up downstream resources:

```csharp
builder.AddProject<MyApp>("api")
       .WithReference(supabase)
       .WithDbContext<MyDbContext>((sp, options) =>
           options.UseNpgsql(
               supabase.ConnectionStringExpression.GetValueAsync().Result));
```

## License

Licensed under the [MIT License](../../LICENSE).

