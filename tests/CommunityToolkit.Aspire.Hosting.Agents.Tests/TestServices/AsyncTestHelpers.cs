namespace CommunityToolkit.Aspire.Hosting.Agents.Tests;

internal static class AsyncTestHelpers
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public static Task DefaultTimeout(this Task task) => task.WaitAsync(Timeout);
    public static Task DefaultTimeout(this Task task, TimeSpan timeout) => task.WaitAsync(timeout);
    public static Task<T> DefaultTimeout<T>(this Task<T> task) => task.WaitAsync(Timeout);
    public static Task<T> DefaultTimeout<T>(this Task<T> task, TimeSpan timeout) => task.WaitAsync(timeout);
    public static Task<T> DefaultTimeout<T>(this ValueTask<T> task) => task.AsTask().WaitAsync(Timeout);
}
