using Quartz;
using QuartzSample.ApiService.Jobs;

namespace QuartzSample.ApiService.Services;

/// <summary>
/// Service for scheduling Quartz jobs with proper configuration
/// </summary>
public class QuartzJobScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<QuartzJobScheduler> _logger;

    public QuartzJobScheduler(
        ISchedulerFactory schedulerFactory,
        ILogger<QuartzJobScheduler> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Schedule a one-time job
    /// </summary>
    public async Task<string> ScheduleOneTimeJobAsync<TJob>(
        JobDataMap jobData,
        DateTimeOffset startTime,
        string? jobId = null) where TJob : IJob
    {
        var scheduler = await _schedulerFactory.GetScheduler();

        jobId ??= Guid.NewGuid().ToString();
        var jobKey = new JobKey(jobId, typeof(TJob).Name);

        var job = JobBuilder.Create<TJob>()
            .WithIdentity(jobKey)
            .SetJobData(jobData)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{jobId}-trigger", typeof(TJob).Name)
            .StartAt(startTime)
            .Build();

        await scheduler.ScheduleJob(job, trigger);

        _logger.LogInformation(
            "Scheduled one-time job {JobType} with ID {JobId} at {StartTime}",
            typeof(TJob).Name, jobId, startTime);

        return jobId;
    }

    /// <summary>
    /// Schedule a recurring job with cron expression
    /// </summary>
    public async Task<string> ScheduleRecurringJobAsync<TJob>(
        JobDataMap jobData,
        string cronExpression,
        string? jobId = null) where TJob : IJob
    {
        var scheduler = await _schedulerFactory.GetScheduler();

        jobId ??= Guid.NewGuid().ToString();
        var jobKey = new JobKey(jobId, typeof(TJob).Name);

        var job = JobBuilder.Create<TJob>()
            .WithIdentity(jobKey)
            .SetJobData(jobData)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{jobId}-trigger", typeof(TJob).Name)
            .WithCronSchedule(cronExpression)
            .Build();

        await scheduler.ScheduleJob(job, trigger);

        _logger.LogInformation(
            "Scheduled recurring job {JobType} with ID {JobId} using cron: {CronExpression}",
            typeof(TJob).Name, jobId, cronExpression);

        return jobId;
    }

    /// <summary>
    /// Schedule a simple repeating job
    /// </summary>
    public async Task<string> ScheduleRepeatingJobAsync<TJob>(
        JobDataMap jobData,
        TimeSpan interval,
        int? repeatCount = null,
        string? jobId = null) where TJob : IJob
    {
        var scheduler = await _schedulerFactory.GetScheduler();

        jobId ??= Guid.NewGuid().ToString();
        var jobKey = new JobKey(jobId, typeof(TJob).Name);

        var job = JobBuilder.Create<TJob>()
            .WithIdentity(jobKey)
            .SetJobData(jobData)
            .Build();

        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity($"{jobId}-trigger", typeof(TJob).Name)
            .StartNow()
            .WithSimpleSchedule(x =>
            {
                x.WithInterval(interval);
                if (repeatCount.HasValue)
                    x.WithRepeatCount(repeatCount.Value);
                else
                    x.RepeatForever();
            });

        var trigger = triggerBuilder.Build();

        await scheduler.ScheduleJob(job, trigger);

        _logger.LogInformation(
            "Scheduled repeating job {JobType} with ID {JobId} every {Interval}",
            typeof(TJob).Name, jobId, interval);

        return jobId;
    }

    /// <summary>
    /// Cancel a scheduled job
    /// </summary>
    public async Task<bool> CancelJobAsync(string jobId, string jobGroup)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(jobId, jobGroup);

        var result = await scheduler.DeleteJob(jobKey);

        if (result)
        {
            _logger.LogInformation("Cancelled job {JobId} in group {JobGroup}", jobId, jobGroup);
        }

        return result;
    }

    /// <summary>
    /// Get all scheduled jobs
    /// </summary>
    public async Task<List<JobInfo>> GetAllJobsAsync()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobGroups = await scheduler.GetJobGroupNames();
        var jobs = new List<JobInfo>();

        foreach (var group in jobGroups)
        {
            var jobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(group));

            foreach (var jobKey in jobKeys)
            {
                var jobDetail = await scheduler.GetJobDetail(jobKey);
                var triggers = await scheduler.GetTriggersOfJob(jobKey);

                if (jobDetail != null)
                {
                    jobs.Add(new JobInfo
                    {
                        JobId = jobKey.Name,
                        JobGroup = jobKey.Group,
                        JobType = jobDetail.JobType.Name,
                        Description = jobDetail.Description,
                        NextFireTime = triggers.FirstOrDefault()?.GetNextFireTimeUtc()?.DateTime,
                        PreviousFireTime = triggers.FirstOrDefault()?.GetPreviousFireTimeUtc()?.DateTime
                    });
                }
            }
        }

        return jobs;
    }
}

public record JobInfo
{
    public string JobId { get; init; } = string.Empty;
    public string JobGroup { get; init; } = string.Empty;
    public string JobType { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime? NextFireTime { get; init; }
    public DateTime? PreviousFireTime { get; init; }
}
