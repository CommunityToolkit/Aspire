# CommunityToolkit.Aspire.Neon library

Provides client extensions for connecting to Neon Postgres using Npgsql in .NET Aspire services.

## Getting Started

### Install the package

In your service project, install the package using the following command:

```shell
dotnet add package CommunityToolkit.Aspire.Neon
```

### Example usage

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNeonClient("neon");

var app = builder.Build();

app.MapGet("/healthz", () => "ok");

app.MapDefaultEndpoints();

app.Run();
```

## What this package does

- Registers `NpgsqlDataSource` for the named connection string.
- Optionally adds a `NpgSqlHealthCheck` when health checks are enabled.
- Reads settings from the `Aspire:Neon:Client` configuration section.

If you already configure `NpgsqlDataSource` yourself, this package is optional. The hosting integration only supplies the connection string; client registration remains up to the service.

## Publish mode support

When deploying with Docker Compose, ACA, or other container orchestrators, the Neon provisioner writes connection strings to `.env` files on a shared Docker volume. **Project consumers** (i.e., services created with `AddProject<T>`) cannot have their container entrypoint overridden, so two options are available:

### Option 1: `AddNeonConnectionStrings()` (recommended for most apps)

Reads all Neon provisioner `.env` files and registers their connection URIs as standard `ConnectionStrings:{name}` entries. Call this early in `Program.cs`, before any service that reads connection strings:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddNeonConnectionStrings();

// Connection strings from Neon are now available:
string connectionString = builder.Configuration.GetConnectionString("appdb")!;
builder.Services.AddNpgsqlDataSource(connectionString);

// Or use Entity Framework Core:
builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("appdb")));
```

### Option 2: `AddNeonClient(connectionName)`

If you want both connection string resolution **and** `NpgsqlDataSource` registration, use `AddNeonClient`. It automatically resolves provisioner `.env` files when needed:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddNeonClient("appdb");

// NpgsqlDataSource is now registered and ready to use.
```

> **Note:** `AddNeonClient` includes the env-file resolution automatically — calling `AddNeonConnectionStrings` separately is unnecessary when using `AddNeonClient`.

### How it works

In publish mode, the hosting integration's `.WithReference(database)` injects two environment variables into project consumers:

| Variable | Example value | Purpose |
| --- | --- | --- |
| `NEON_OUTPUT_DIR` | `/neon-output` | Shared volume mount path |
| `NEON_ENV_FILE__{name}` | `/neon-output/appdb.env` | Path to the database-specific `.env` file |

The client methods read these files and extract the `NEON_CONNECTION_URI` value, which is then registered as `ConnectionStrings:{name}` in the application configuration.

## Additional Information

- https://neon.tech/docs

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
