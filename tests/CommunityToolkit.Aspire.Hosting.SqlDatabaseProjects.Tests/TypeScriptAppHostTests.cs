using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.Tests;

[RequiresContainerRuntime]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.SqlDatabaseProjects",
            exampleName: "sql-database-projects",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}