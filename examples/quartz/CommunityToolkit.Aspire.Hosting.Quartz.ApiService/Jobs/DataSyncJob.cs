using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// Data sync job - demonstrates stateful job with progress tracking
/// </summary>
[DisallowConcurrentExecution]
[PersistJobDataAfterExecution]
public class DataSyncJob : IJob
{
    private readonly ILogger<DataSyncJob> _logger;

    public const string Name = nameof(DataSyncJob);
    public const string Group = "integration";

    public DataSyncJob(ILogger<DataSyncJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var source = data.GetString("source") ?? "SourceDB";
        var destination = data.GetString("destination") ?? "DestinationDB";
        var batchSize = data.GetInt("batchSize");
        if (batchSize == 0) batchSize = 1000;

        var lastSyncId = data.GetLong("lastSyncId");

        _logger.LogInformation(
            "🔄 Starting data sync from {Source} to {Destination} (Last ID: {LastId})",
            source, destination, lastSyncId);

        // Simulate data sync
        var recordsSynced = 0;
        for (int batch = 0; batch < 5; batch++)
        {
            await Task.Delay(800, context.CancellationToken);
            recordsSynced += batchSize;
            _logger.LogInformation("Synced batch {Batch}: {Records} records", batch + 1, recordsSynced);
        }

        // Update state for next run
        data.Put("lastSyncId", lastSyncId + recordsSynced);
        data.Put("lastSyncTime", DateTime.UtcNow.ToString("O"));
        data.Put("totalRecordsSynced", data.GetLong("totalRecordsSynced") + recordsSynced);

        _logger.LogInformation(
            "✅ Sync completed - {Records} records synced. Total: {Total}",
            recordsSynced, data.GetLong("totalRecordsSynced"));
    }
}
