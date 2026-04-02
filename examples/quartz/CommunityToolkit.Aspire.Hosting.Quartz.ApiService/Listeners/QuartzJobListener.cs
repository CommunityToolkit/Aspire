using Microsoft.AspNetCore.SignalR;
using Quartz;
using QuartzSample.ApiService.Hubs;

namespace QuartzSample.ApiService.Listeners;

/// <summary>
/// Quartz job listener that broadcasts events via SignalR
/// </summary>
public class QuartzJobListener : IJobListener
{
    private readonly IHubContext<QuartzHub> _hubContext;
    private readonly ILogger<QuartzJobListener> _logger;

    public string Name => "SignalRJobListener";

    public QuartzJobListener(
        IHubContext<QuartzHub> hubContext,
        ILogger<QuartzJobListener> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var jobKey = context.JobDetail.Key;
        _logger.LogInformation("Job {JobName} is about to execute", jobKey.Name);

        await _hubContext.Clients.All.SendAsync("JobStarted", new
        {
            jobId = jobKey.Name,
            jobType = context.JobDetail.JobType.Name,
            timestamp = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var jobKey = context.JobDetail.Key;
        _logger.LogWarning("Job {JobName} execution was vetoed", jobKey.Name);

        await _hubContext.Clients.All.SendAsync("JobVetoed", new
        {
            jobId = jobKey.Name,
            jobType = context.JobDetail.JobType.Name,
            timestamp = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        var jobKey = context.JobDetail.Key;
        var success = jobException == null;

        _logger.LogInformation(
            "Job {JobName} executed with result: {Success}",
            jobKey.Name,
            success ? "Success" : "Failed");

        await _hubContext.Clients.All.SendAsync("JobCompleted", new
        {
            jobId = jobKey.Name,
            jobType = context.JobDetail.JobType.Name,
            success,
            error = jobException?.Message,
            duration = context.JobRunTime.TotalSeconds,
            timestamp = DateTime.UtcNow
        }, cancellationToken);
    }
}
