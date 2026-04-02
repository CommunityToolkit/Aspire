using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// File cleanup job - demonstrates file system operations
/// </summary>
[DisallowConcurrentExecution]
public class FileCleanupJob : IJob
{
    private readonly ILogger<FileCleanupJob> _logger;

    public const string Name = nameof(FileCleanupJob);
    public const string Group = "maintenance";

    public FileCleanupJob(ILogger<FileCleanupJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var directory = data.GetString("directory") ?? "/temp";
        var daysOld = data.GetInt("daysOld");
        if (daysOld == 0) daysOld = 7;
        var filePattern = data.GetString("filePattern") ?? "*.*";

        _logger.LogInformation(
            "🗑️ Cleaning up files in {Directory} older than {Days} days (Pattern: {Pattern})",
            directory, daysOld, filePattern);

        // Simulate file cleanup
        await Task.Delay(2000, context.CancellationToken);

        var filesDeleted = Random.Shared.Next(5, 50);
        var spaceFreed = Random.Shared.Next(100, 1000);

        _logger.LogInformation(
            "✅ Cleanup completed - Deleted {Files} files, Freed {Space}MB",
            filesDeleted, spaceFreed);
    }
}
