using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.DuckDB;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.DuckDB.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.DuckDB",
            exampleName: "duckdb",
            waitForResources: ["analytics", "api"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
