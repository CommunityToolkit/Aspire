namespace Aspire.Quartz;

/// <summary>
/// Constants used throughout the Quartz integration.
/// </summary>
internal static class QuartzConstants
{
    /// <summary>
    /// Default scheduler name used by Aspire Quartz integration.
    /// </summary>
    public const string DefaultSchedulerName = "AspireQuartzScheduler";

    /// <summary>
    /// Default job group name.
    /// </summary>
    public const string DefaultJobGroup = "DEFAULT";

    /// <summary>
    /// Default trigger group name.
    /// </summary>
    public const string DefaultTriggerGroup = "DEFAULT";

    /// <summary>
    /// Default connection name for Quartz database.
    /// </summary>
    public const string DefaultConnectionName = "quartz";

    /// <summary>
    /// Default idempotency key expiration (7 days).
    /// </summary>
    public static readonly TimeSpan DefaultIdempotencyExpiration = TimeSpan.FromDays(7);

    /// <summary>
    /// Default job priority.
    /// </summary>
    public const int DefaultPriority = 5;

    /// <summary>
    /// Default max concurrency for job execution.
    /// </summary>
    public const int DefaultMaxConcurrency = 10;
}
