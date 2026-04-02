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
- Multi-database support (PostgreSQL, SQL Server)
- Automatic schema migrations on startup

## Database Schema

The integration automatically creates Quartz.NET database tables on first run using the built-in `QuartzMigrationService`. No manual migrations or EF Core required - just reference the database and the tables will be created automatically:

- QRTZ_JOB_DETAILS
- QRTZ_TRIGGERS
- QRTZ_SIMPLE_TRIGGERS
- QRTZ_CRON_TRIGGERS
- QRTZ_FIRED_TRIGGERS
- And more...

## Documentation

Visit [aspire.dev](https://aspire.dev) for complete documentation.

## License

MIT License - See [LICENSE](../../LICENSE)
