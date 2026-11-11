using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.AppHost.Go",
            exampleName: "sql-database-projects",
            // The prebuilt Windows AppHost server fails to load hostfxr for MSBuildLocator.
            validatePublish: !OperatingSystem.IsWindows(),
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
