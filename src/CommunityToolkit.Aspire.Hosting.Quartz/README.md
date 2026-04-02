# AspireQuartz.Hosting

Hosting library for background job scheduling in .NET Aspire using Quartz.NET.

## Installation

```bash
dotnet add package AspireQuartz.Hosting
```

## Quick Start

```csharp
// In your AppHost
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("quartzdb");

builder.AddProject<Projects.ApiService>("api")
    .WithReference(postgres);

builder.Build().Run();
```

## Features

- Aspire resource pattern integration
- Automatic connection string injection
- Health checks
- OpenTelemetry metrics
- Multi-database support (PostgreSQL, SQL Server, MySQL, SQLite)

## Documentation

Visit [GitHub Repository](https://github.com/alnuaimicoder/aspire-hosting-quartz) for full documentation.

## License

MIT License - See [LICENSE](../../LICENSE)
