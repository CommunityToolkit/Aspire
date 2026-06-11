namespace Aspire.Quartz;

/// <summary>
/// Fluent builder for creating retry policies.
/// </summary>
public sealed class RetryPolicyBuilder
{
    private int _maxAttempts = 3;
    private TimeSpan _initialDelay = TimeSpan.FromSeconds(5);
    private BackoffStrategy _strategy = BackoffStrategy.Exponential;
    private double _multiplier = 2.0;
    private TimeSpan _maxDelay = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Sets the maximum number of retry attempts.
    /// </summary>
    public RetryPolicyBuilder WithMaxAttempts(int attempts)
    {
        if (attempts < 1)
            throw new ArgumentException("Max attempts must be at least 1", nameof(attempts));

        _maxAttempts = attempts;
        return this;
    }

    /// <summary>
    /// Configures exponential backoff strategy.
    /// </summary>
    public RetryPolicyBuilder WithExponentialBackoff(TimeSpan initialDelay, double multiplier = 2.0)
    {
        if (initialDelay <= TimeSpan.Zero)
            throw new ArgumentException("Initial delay must be positive", nameof(initialDelay));
        if (multiplier <= 1.0)
            throw new ArgumentException("Multiplier must be greater than 1", nameof(multiplier));

        _strategy = BackoffStrategy.Exponential;
        _initialDelay = initialDelay;
        _multiplier = multiplier;
        return this;
    }

    /// <summary>
    /// Configures linear backoff strategy.
    /// </summary>
    public RetryPolicyBuilder WithLinearBackoff(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
            throw new ArgumentException("Delay must be positive", nameof(delay));

        _strategy = BackoffStrategy.Linear;
        _initialDelay = delay;
        return this;
    }

    /// <summary>
    /// Sets the maximum delay between retries.
    /// </summary>
    public RetryPolicyBuilder WithMaxDelay(TimeSpan maxDelay)
    {
        if (maxDelay <= TimeSpan.Zero)
            throw new ArgumentException("Max delay must be positive", nameof(maxDelay));

        _maxDelay = maxDelay;
        return this;
    }

    /// <summary>
    /// Builds the retry policy.
    /// </summary>
    public RetryPolicy Build() => new()
    {
        MaxAttempts = _maxAttempts,
        InitialDelay = _initialDelay,
        Strategy = _strategy,
        BackoffMultiplier = _multiplier,
        MaxDelay = _maxDelay
    };
}
