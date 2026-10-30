using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects;

internal sealed class SqlProjectPathMetadata(string projectPath) : IProjectMetadata
{
    public string ProjectPath { get; } = projectPath;
}

internal sealed class SqlProjectBuildAnnotation(string configuration) : IResourceAnnotation
{
    public string Configuration { get; } = configuration;

    public SemaphoreSlim BuildLock { get; } = new(1, 1);
}