using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

internal sealed record SqlPackageRestoreAnnotation(string PackageId, string Version, string AppHostDirectory) : IResourceAnnotation
{
    public string? DacpacRelativePath { get; init; }

    public SemaphoreSlim RestoreLock { get; } = new(1, 1);
}