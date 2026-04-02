namespace Aspire.Quartz;

/// <summary>
/// Defines retry behavior for failed jobs.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// Default is 3.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay before first retry.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the backoff strategy (Linear, Exponential).
    /// Default is Exponential.
    /// </summary>
    public BackoffStrategy Strategy { get; set; } = BackoffStrategy.Exponential;

    /// <summary>
    /// Gets or sets the multiplier for exponential backoff.
    /// Default is 2.0.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// Default is 30 minutes.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Defines the backoff strategy for retry delays.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// Linear backoff - same delay between each retry.
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential backoff - delay increases exponentially with each retry.
    /// </summary>
    Exponential
}
