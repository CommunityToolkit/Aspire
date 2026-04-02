namespace Aspire.Quartz;

/// <summary>
/// Provides context information to executing jobs.
/// </summary>
public sealed class JobContext
{
    /// <summary>
    /// Gets the unique identifier for this job execution.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Gets the type name of the job being executed.
    /// </summary>
    public required string JobType { get; init; }

    /// <summary>
    /// Gets the parameters passed to the job.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }

    /// <summary>
    /// Gets the current retry attempt number (0 for first attempt).
    /// </summary>
    public required int RetryAttempt { get; init; }

    /// <summary>
    /// Gets the time when the job was scheduled to run.
    /// </summary>
    public required DateTimeOffset ScheduledTime { get; init; }

    /// <summary>
    /// Gets the time when the job execution started.
    /// </summary>
    public required DateTimeOffset StartTime { get; init; }
}
