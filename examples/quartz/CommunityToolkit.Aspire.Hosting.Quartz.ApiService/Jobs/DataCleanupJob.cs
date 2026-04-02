using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// Data cleanup job - runs every Sunday at 3:00 AM
/// </summary>
[DisallowConcurrentExecution]
public class DataCleanupJob : IJob
{
    private readonly ILogger<DataCleanupJob> _logger;

    public const string Name = nameof(DataCleanupJob);
    public const string Group = "maintenance";

    public DataCleanupJob(ILogger<DataCleanupJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var daysToKeep = data.GetInt("daysToKeep");
        var tableName = data.GetString("tableName") ?? "logs";

        _logger.LogInformation(
            "Cleaning up {TableName} older than {Days} days",
            tableName, daysToKeep);

        // Simulate cleanup
        await Task.Delay(3000, context.CancellationToken);

        _logger.LogInformation("Cleanup completed successfully");
    }
}
