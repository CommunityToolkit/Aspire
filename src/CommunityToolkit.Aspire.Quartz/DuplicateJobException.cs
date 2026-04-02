namespace Aspire.Quartz;

/// <summary>
/// Exception thrown when attempting to enqueue a job with a duplicate idempotency key.
/// </summary>
public sealed class DuplicateJobException : Exception
{
    /// <summary>
    /// Gets the idempotency key that caused the duplicate.
    /// </summary>
    public string IdempotencyKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateJobException"/> class.
    /// </summary>
    /// <param name="idempotencyKey">The duplicate idempotency key.</param>
    public DuplicateJobException(string idempotencyKey)
        : base($"A job with idempotency key '{idempotencyKey}' has already been enqueued and has not expired yet. " +
               "To enqueue a new job with the same key, wait for the previous job to complete and expire, or use a different idempotency key.")
    {
        IdempotencyKey = idempotencyKey;
    }
}
