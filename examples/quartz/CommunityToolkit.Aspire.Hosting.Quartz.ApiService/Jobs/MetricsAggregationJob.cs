using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// Metrics aggregation job - demonstrates data processing
/// </summary>
[DisallowConcurrentExecution]
public class MetricsAggregationJob : IJob
{
    private readonly ILogger<MetricsAggregationJob> _logger;

    public const string Name = nameof(MetricsAggregationJob);
    public const string Group = "analytics";

    public MetricsAggregationJob(ILogger<MetricsAggregationJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var metricType = data.GetString("metricType") ?? "requests";
        var aggregationPeriod = data.GetString("aggregationPeriod") ?? "hourly";

        _logger.LogInformation(
            "📊 Aggregating {MetricType} metrics for {Period} period",
            metricType, aggregationPeriod);

        // Simulate metrics processing
        await Task.Delay(1500, context.CancellationToken);

        var totalRequests = Random.Shared.Next(1000, 10000);
        var avgResponseTime = Random.Shared.Next(50, 500);
        var errorRate = Random.Shared.NextDouble() * 5;

        _logger.LogInformation(
            "✅ Metrics aggregated - Requests: {Requests}, Avg Response: {AvgTime}ms, Error Rate: {ErrorRate:F2}%",
            totalRequests, avgResponseTime, errorRate);
    }
}
