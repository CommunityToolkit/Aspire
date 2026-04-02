using Quartz;

namespace QuartzSample.ApiService.Jobs;

/// <summary>
/// Daily report generation job - runs every day at 2:00 AM
/// </summary>
[DisallowConcurrentExecution]
public class DailyReportJob : IJob
{
    private readonly ILogger<DailyReportJob> _logger;

    public const string Name = nameof(DailyReportJob);
    public const string Group = "reports";

    public DailyReportJob(ILogger<DailyReportJob> logger)
    {
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var reportType = data.GetString("reportType") ?? "daily";
        var recipients = data.GetString("recipients") ?? "admin@example.com";

        _logger.LogInformation(
            "Generating {ReportType} report for {Recipients} at {Time}",
            reportType, recipients, DateTime.UtcNow);

        // Simulate report generation
        await Task.Delay(2000, context.CancellationToken);

        _logger.LogInformation("Report generated successfully");
    }
}
