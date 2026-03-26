using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Sqlite.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Sqlite.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Sqlite",
            exampleName: "sqlite",
            waitForResources: ["sqlite", "sqlite-default", "sqlite-browser", "sqlite-default-sqliteweb"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}