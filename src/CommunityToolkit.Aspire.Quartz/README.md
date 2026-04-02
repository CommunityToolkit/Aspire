# AspireQuartz

Client library for background job scheduling in .NET Aspire using Quartz.NET.

## Installation

```bash
dotnet add package AspireQuartz
```

## Quick Start

```csharp
// In your API/Service
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
- SQL Server & PostgreSQL support

## Documentation

Visit [GitHub Repository](https://github.com/alnuaimicoder/aspire-hosting-quartz) for full documentation.

## License

MIT License - See [LICENSE](../../LICENSE)
