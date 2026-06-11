# CommunityToolkit.Aspire.Quartz.Abstractions

Core abstractions and contracts for Quartz.NET background job scheduling in .NET Aspire.

## Installation

```bash
dotnet add package CommunityToolkit.Aspire.Quartz.Abstractions
```

## What's Included

- `IBackgroundJobClient` - Interface for job scheduling
- `JobOptions` - Configuration for job execution
- `RetryPolicy` - Retry configuration
- `JobContext` - Execution context

Jobs should implement `Quartz.IJob` directly.

## Usage

```csharp
using Quartz;

public class SendEmailJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var email = context.JobDetail.JobDataMap.GetString("email");
        // Your job logic here
        await Task.CompletedTask;
    }
}
```

## Documentation

Visit [aspire.dev](https://aspire.dev) for complete documentation.

## License

MIT License - See [LICENSE](../../LICENSE)
