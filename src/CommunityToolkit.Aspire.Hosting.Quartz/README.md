# CommunityToolkit.Aspire.Hosting.Quartz

An Aspire hosting integration for Quartz.NET background job scheduling with persistent storage, automatic migrations, health checks, and OpenTelemetry metrics.

## Installation

```bash
dotnet add package CommunityToolkit.Aspire.Hosting.Quartz
```

## Usage

### AppHost Configuration

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
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
- Automatic schema migrations using EF Core

## Documentation

Visit [aspire.dev](https://aspire.dev) for complete documentation.

## License

MIT License - See [LICENSE](../../LICENSE)
