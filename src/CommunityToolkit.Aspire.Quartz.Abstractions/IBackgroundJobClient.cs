namespace Aspire.Quartz;

/// <summary>
/// Defines the contract for enqueuing and scheduling background jobs.
/// </summary>
public interface IBackgroundJobClient
{
    /// <summary>
    /// Enqueues a job for immediate execution.
    /// </summary>
    /// <typeparam name="TJob">The type of job to execute.</typeparam>
    /// <param name="parameters">Optional parameters to pass to the job.</param>
    /// <param name="options">Optional job configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A unique job identifier.</returns>
    Task<string> EnqueueAsync<TJob>(
        object? parameters = null,
        JobOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : IJob;

    /// <summary>
    /// Schedules a job for delayed execution.
    /// </summary>
    /// <typeparam name="TJob">The type of job to execute.</typeparam>
    /// <param name="delay">The delay before execution.</param>
    /// <param name="parameters">Optional parameters to pass to the job.</param>
    /// <param name="options">Optional job configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A unique job identifier.</returns>
    Task<string> ScheduleAsync<TJob>(
        TimeSpan delay,
        object? parameters = null,
        JobOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : IJob;

    /// <summary>
    /// Schedules a job with a cron expression.
    /// </summary>
    /// <typeparam name="TJob">The type of job to execute.</typeparam>
    /// <param name="cronExpression">The cron expression defining the schedule.</param>
    /// <param name="parameters">Optional parameters to pass to the job.</param>
    /// <param name="options">Optional job configuration options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A unique job identifier.</returns>
    Task<string> ScheduleAsync<TJob>(
        string cronExpression,
        object? parameters = null,
        JobOptions? options = null,
        CancellationToken cancellationToken = default)
        where TJob : IJob;
}
