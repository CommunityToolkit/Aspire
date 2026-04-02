using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aspire.Quartz;

/// <summary>
/// Background job client that uses Quartz.NET ISchedulerFactory API properly.
/// </summary>
internal sealed class BackgroundJobClient : IBackgroundJobClient
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<BackgroundJobClient> _logger;
    private readonly ActivitySource _activitySource;
    private readonly IIdempotencyStore _idempotencyStore;

    public BackgroundJobClient(
        ISchedulerFactory schedulerFactory,
        ILogger<BackgroundJobClient> logger,
        ActivitySource activitySource,
        IIdempotencyStore idempotencyStore)
    {
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
    }

    public async Task<string> EnqueueAsync<TJob>(
        object? parameters = null,
        JobOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : IJob
    {
        using var activity = _activitySource.StartActivity("job.enqueue");

        var jobId = Guid.NewGuid().ToString();
        var jobGroup = QuartzConstants.DefaultJobGroup;

        activity?.SetTag("job.id", jobId);
        activity?.SetTag("job.type", typeof(TJob).Name);

        // Check idempotency
        if (options?.IdempotencyKey != null)
        {
            if (!await _idempotencyStore.TryAcquireAsync(options.IdempotencyKey, jobId, cancellationToken))
            {
                _logger.LogWarning("Duplicate job rejected: {IdempotencyKey}", options.IdempotencyKey);
                throw new DuplicateJobException(options.IdempotencyKey);
            }
        }

        // Get scheduler from factory
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        // Build job using Quartz.NET API (proper way)
        var jobBuilder = JobBuilder.Create(typeof(TJob))
            .WithIdentity(jobId, jobGroup)
            .StoreDurably();

        // Add parameters as job data
        if (parameters != null)
        {
            var jobDataMap = ConvertToJobDataMap(parameters);
            jobBuilder.SetJobData(jobDataMap);
        }

        var job = jobBuilder.Build();

        // Build trigger to run immediately
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{jobId}_trigger", QuartzConstants.DefaultTriggerGroup)
            .ForJob(job)
            .StartNow()
            .Build();

        // Schedule using Quartz.NET API (proper way)
        await scheduler.ScheduleJob(job, trigger, cancellationToken);

        _logger.LogInformation("Job enqueued: {JobId} ({JobType})", jobId, typeof(TJob).Name);

        return jobId;
    }

    public async Task<string> ScheduleAsync<TJob>(
        TimeSpan delay,
        object? parameters = null,
        JobOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : IJob
    {
        using var activity = _activitySource.StartActivity("job.schedule");

        var jobId = Guid.NewGuid().ToString();
        var jobGroup = QuartzConstants.DefaultJobGroup;
        var scheduledTime = DateTimeOffset.UtcNow.Add(delay);

        activity?.SetTag("job.id", jobId);
        activity?.SetTag("job.type", typeof(TJob).Name);
        activity?.SetTag("job.scheduled_time", scheduledTime);

        // Check idempotency
        if (options?.IdempotencyKey != null)
        {
            if (!await _idempotencyStore.TryAcquireAsync(options.IdempotencyKey, jobId, cancellationToken))
            {
                _logger.LogWarning("Duplicate job rejected: {IdempotencyKey}", options.IdempotencyKey);
                throw new DuplicateJobException(options.IdempotencyKey);
            }
        }

        // Get scheduler from factory
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        // Build job using Quartz.NET API
        var jobBuilder = JobBuilder.Create(typeof(TJob))
            .WithIdentity(jobId, jobGroup)
            .StoreDurably();

        if (parameters != null)
        {
            var jobDataMap = ConvertToJobDataMap(parameters);
            jobBuilder.SetJobData(jobDataMap);
        }

        var job = jobBuilder.Build();

        // Build trigger to run at scheduled time
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{jobId}_trigger", QuartzConstants.DefaultTriggerGroup)
            .ForJob(job)
            .StartAt(scheduledTime)
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);

        _logger.LogInformation("Job scheduled: {JobId} ({JobType}) at {ScheduledTime}",
            jobId, typeof(TJob).Name, scheduledTime);

        return jobId;
    }

    public async Task<string> ScheduleAsync<TJob>(
        string cronExpression,
        object? parameters = null,
        JobOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : IJob
    {
        using var activity = _activitySource.StartActivity("job.schedule.cron");

        // Validate cron expression
        CronExpressionValidator.Validate(cronExpression);

        var jobId = Guid.NewGuid().ToString();
        var jobGroup = QuartzConstants.DefaultJobGroup;

        activity?.SetTag("job.id", jobId);
        activity?.SetTag("job.type", typeof(TJob).Name);
        activity?.SetTag("job.cron_expression", cronExpression);

        // Check idempotency
        if (options?.IdempotencyKey != null)
        {
            if (!await _idempotencyStore.TryAcquireAsync(options.IdempotencyKey, jobId, cancellationToken))
            {
                _logger.LogWarning("Duplicate job rejected: {IdempotencyKey}", options.IdempotencyKey);
                throw new DuplicateJobException(options.IdempotencyKey);
            }
        }

        // Get scheduler from factory
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

        // Build job using Quartz.NET API
        var jobBuilder = JobBuilder.Create(typeof(TJob))
            .WithIdentity(jobId, jobGroup)
            .StoreDurably();

        if (parameters != null)
        {
            var jobDataMap = ConvertToJobDataMap(parameters);
            jobBuilder.SetJobData(jobDataMap);
        }

        var job = jobBuilder.Build();

        // Build cron trigger
        var trigger = TriggerBuilder.Create()
            .WithIdentity($"{jobId}_trigger", QuartzConstants.DefaultTriggerGroup)
            .ForJob(job)
            .WithCronSchedule(cronExpression)
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);

        _logger.LogInformation("Job scheduled with cron: {JobId} ({JobType}) - {CronExpression}",
            jobId, typeof(TJob).Name, cronExpression);

        return jobId;
    }

    /// <summary>
    /// Converts parameters object to JobDataMap.
    /// </summary>
    private static JobDataMap ConvertToJobDataMap(object parameters)
    {
        var jobDataMap = new JobDataMap();

        // Use reflection to convert object properties to JobDataMap
        var properties = parameters.GetType().GetProperties();
        foreach (var prop in properties)
        {
            var value = prop.GetValue(parameters);
            if (value != null)
            {
                jobDataMap[prop.Name] = value;
            }
        }

        return jobDataMap;
    }
}
