namespace CommunityToolkit.Aspire.Testing;

/// <summary>
/// Serializes the polyglot AppHost flows that run in the same test process.
/// </summary>
/// <remarks>
/// The Go and TypeScript AppHosts of an example reference the same integration project, and the Aspire CLI
/// builds that project in place. Two flows running at once start two builds that write the same files
/// under <c>bin</c> and <c>obj</c>.
/// </remarks>
internal static class PolyglotAppHostLock
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    /// <summary>
    /// Waits for the flow that holds the lock, if any, and takes the lock.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    public static async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await Semaphore.WaitAsync(cancellationToken);
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        public void Dispose() => Semaphore.Release();
    }
}
