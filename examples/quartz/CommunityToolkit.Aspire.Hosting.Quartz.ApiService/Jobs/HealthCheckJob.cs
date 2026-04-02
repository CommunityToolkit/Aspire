using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// Health check job - runs every 5 minutes
/// </summary>
public class HealthCheckJob : IJob
{
    private readonly ILogger<HealthCheckJob> _logger;

    public const string Name = nameof(HealthCheckJob);
    public const string Group = "monitoring";

    public HealthCheckJob(ILogger<HealthCheckJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var endpoint = data.GetString("endpoint") ?? "https://api.example.com/health";

        _logger.LogInformation("Checking health of {Endpoint}", endpoint);

        // Simulate health check
        await Task.Delay(500, context.CancellationToken);

        _logger.LogInformation("Health check passed");
    }
}
