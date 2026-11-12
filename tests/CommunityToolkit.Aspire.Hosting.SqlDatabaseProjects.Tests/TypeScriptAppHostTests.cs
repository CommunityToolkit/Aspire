using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await SqlPolyglotRestoreDiagnostics.Run("CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.AppHost.TypeScript", () => TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects",
            exampleName: "sql-database-projects",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken));
    }
}