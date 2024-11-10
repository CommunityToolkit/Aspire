using Aspire.Hosting;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

internal class TestProject : IProjectMetadata
{
    public const string RelativePath = "../../../../../examples/sql-database-projects/SdkProject/SdkProject.csproj";

    public string ProjectPath { get; } = RelativePath;
}
