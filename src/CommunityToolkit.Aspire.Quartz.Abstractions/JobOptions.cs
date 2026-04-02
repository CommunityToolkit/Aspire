namespace Aspire.Quartz;

/// <summary>
/// Options for configuring individual job behavior.
/// </summary>
public sealed class JobOptions
{
    /// <summary>
    /// Gets or sets the idempotency key to prevent duplicate execution.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the retry policy for failed jobs.
    /// </summary>
    public RetryPolicy? RetryPolicy { get; set; }

    /// <summary>
    /// Gets or sets the priority for job execution (higher = more priority).
    /// Default is 5.
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Gets or sets custom tags for filtering and monitoring.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }
}
