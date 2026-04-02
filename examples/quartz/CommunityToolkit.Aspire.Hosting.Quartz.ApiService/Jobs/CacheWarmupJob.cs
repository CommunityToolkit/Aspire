using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// Cache warmup job - demonstrates startup job
/// </summary>
public class CacheWarmupJob : IJob
{
    private readonly ILogger<CacheWarmupJob> _logger;

    public const string Name = nameof(CacheWarmupJob);
    public const string Group = "performance";

    public CacheWarmupJob(ILogger<CacheWarmupJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var cacheKeys = data.GetString("cacheKeys")?.Split(',') ?? Array.Empty<string>();

        _logger.LogInformation("🔥 Warming up cache with {Count} keys", cacheKeys.Length);

        foreach (var key in cacheKeys)
        {
            await Task.Delay(200, context.CancellationToken);
            _logger.LogInformation("Loaded cache key: {Key}", key.Trim());
        }

        _logger.LogInformation("✅ Cache warmup completed");
    }
}
