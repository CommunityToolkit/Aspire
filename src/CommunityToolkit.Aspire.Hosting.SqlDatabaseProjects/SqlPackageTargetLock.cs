using Aspire.Hosting;
using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

/// <summary>
/// Serializes SqlPackage operations that resolve to the same database.
/// </summary>
internal sealed class SqlPackageTargetLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <summary>
    /// Acquires the lock for the resolved target database.
    /// </summary>
    internal async Task<IDisposable> AcquireAsync(
        string connectionString,
        string? targetDatabaseName,
        CancellationToken cancellationToken)
    {
        var key = GetTargetKey(connectionString, targetDatabaseName);
        var semaphore = _locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    internal static string GetTargetKey(string connectionString, string? targetDatabaseName)
    {
        SqlConnectionStringBuilder builder;
        try
        {
            builder = new(connectionString);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            throw new DistributedApplicationException(
                "The resolved SQL target connection string is invalid.",
                ex);
        }

        var database = string.IsNullOrWhiteSpace(targetDatabaseName)
            ? builder.InitialCatalog
            : targetDatabaseName;
        return $"{builder.DataSource.Trim().ToUpperInvariant()}\0{database.Trim().ToUpperInvariant()}";
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}
