using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// Database backup job - demonstrates long-running job
/// </summary>
[DisallowConcurrentExecution]
[PersistJobDataAfterExecution]
public class DatabaseBackupJob : IJob
{
    private readonly ILogger<DatabaseBackupJob> _logger;

    public const string Name = nameof(DatabaseBackupJob);
    public const string Group = "maintenance";

    public DatabaseBackupJob(ILogger<DatabaseBackupJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var databaseName = data.GetString("databaseName") ?? "production_db";
        var backupPath = data.GetString("backupPath") ?? "/backups";
        var compressionLevel = data.GetInt("compressionLevel");

        _logger.LogInformation(
            "💾 Starting backup of database: {DatabaseName} to {BackupPath}",
            databaseName, backupPath);

        // Simulate backup process
        for (int i = 0; i <= 100; i += 20)
        {
            await Task.Delay(500, context.CancellationToken);
            _logger.LogInformation("Backup progress: {Progress}%", i);
        }

        // Update job data to track last backup
        data.Put("lastBackupTime", DateTime.UtcNow.ToString("O"));
        data.Put("backupCount", data.GetInt("backupCount") + 1);

        _logger.LogInformation(
            "✅ Backup completed successfully. Total backups: {Count}",
            data.GetInt("backupCount"));
    }
}
