# CommunityToolkit.Aspire.Quartz

An Aspire client integration for Quartz.NET background job scheduling with idempotency, OpenTelemetry, health checks, and multi-database support.

## Installation

```bash
dotnet add package CommunityToolkit.Aspire.Quartz
```

## Usage

```csharp
// Configure Quartz.NET
builder.Services.AddQuartz(q =>
{
    q.UsePersistentStore(store =>
        store.UsePostgres(builder.Configuration.GetConnectionString("quartzdb")!));
});

builder.Services.AddQuartzHostedService();

// Add Quartz client
builder.Services.AddQuartzClient(builder.Configuration.GetConnectionString("quartzdb"));

// Enqueue a job
await jobClient.EnqueueAsync<SendEmailJob>(
    new { email = "user@example.com" },
    new JobOptions { IdempotencyKey = "email-123" });

// Schedule with delay
await jobClient.ScheduleAsync<SendEmailJob>(
    new { email = "user@example.com" },
    TimeSpan.FromMinutes(5));

// Schedule with cron
await jobClient.ScheduleAsync<SendEmailJob>(
    new { email = "user@example.com" },
    "0 0 9 * * ?"); // Every day at 9 AM
```

## Features

- Enqueue jobs for immediate execution
- Schedule jobs with delay or cron expressions
- Idempotency support
- Retry policies with exponential/linear backoff
- OpenTelemetry distributed tracing
- Multi-database support (PostgreSQL, SQL Server, MySQL, SQLite)

## Documentation

Visit [aspire.dev](https://aspire.dev) for complete documentation.

## License

MIT License - See [LICENSE](../../LICENSE)
