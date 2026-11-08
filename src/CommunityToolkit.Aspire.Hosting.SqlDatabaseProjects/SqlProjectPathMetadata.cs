using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using System.Collections.Concurrent;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

internal sealed class SqlProjectPathMetadata(string projectPath) : IProjectMetadata
{
    public string ProjectPath { get; } = projectPath;
}

internal sealed class SqlProjectBuildAnnotation(string configuration) : IResourceAnnotation
{
    public string Configuration { get; } = configuration;
}

internal static class SqlProjectBuildLock
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public static SemaphoreSlim Get(string projectPath)
        => Locks.GetOrAdd(Path.GetFullPath(projectPath), static _ => new SemaphoreSlim(1, 1));
}