namespace Aspire.Quartz;

/// <summary>
/// Marker interface for background jobs.
/// </summary>
public interface IJob
{
    /// <summary>
    /// Executes the job logic.
    /// </summary>
    /// <param name="context">The job execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteAsync(JobContext context, CancellationToken cancellationToken);
}
