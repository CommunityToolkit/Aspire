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

## Additional Information

- https://neon.tech/docs

## Feedback & contributing

https://github.com/CommunityToolkit/Aspire
