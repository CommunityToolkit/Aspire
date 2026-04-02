# AspireQuartz.Abstractions

Core abstractions for background job scheduling in .NET Aspire using Quartz.NET.

## Installation

```bash
dotnet add package AspireQuartz.Abstractions
```

## What's Included

- `IBackgroundJobClient` - Interface for job scheduling
- `IJob` - Base interface for jobs
- `JobOptions` - Configuration for job execution
- `RetryPolicy` - Retry configuration
- `JobContext` - Execution context

## Usage

```csharp
public class SendEmailJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // Your job logic here
    }
}
```

## Documentation

Visit [GitHub Repository](https://github.com/alnuaimicoder/aspire-hosting-quartz) for full documentation.

## License

MIT License - See [LICENSE](../../LICENSE)
