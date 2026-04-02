using Microsoft.Extensions.Diagnostics.HealthChecks;
using Quartz;

namespace Aspire.Quartz;

/// <summary>
/// Health check for Quartz.NET scheduler.
/// </summary>
public sealed class QuartzHealthCheck : IHealthCheck
{
    private readonly IScheduler _scheduler;

    public QuartzHealthCheck(IScheduler scheduler)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if scheduler is started
            if (!_scheduler.IsStarted)
            {
                return HealthCheckResult.Unhealthy("Quartz scheduler is not started");
            }

            // Check if scheduler is in standby mode
            if (_scheduler.InStandbyMode)
            {
                return HealthCheckResult.Degraded("Quartz scheduler is in standby mode");
            }

            // Check if scheduler is shutdown
            if (_scheduler.IsShutdown)
            {
                return HealthCheckResult.Unhealthy("Quartz scheduler is shutdown");
            }

            // Get scheduler metadata
            var metadata = await _scheduler.GetMetaData(cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["scheduler_name"] = metadata.SchedulerName,
                ["scheduler_instance_id"] = metadata.SchedulerInstanceId,
                ["running_since"] = metadata.RunningSince?.ToString("O") ?? "N/A",
                ["number_of_jobs_executed"] = metadata.NumberOfJobsExecuted,
                ["thread_pool_size"] = metadata.ThreadPoolSize
            };

            return HealthCheckResult.Healthy("Quartz scheduler is running", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to check Quartz scheduler health", ex);
        }
    }
}
