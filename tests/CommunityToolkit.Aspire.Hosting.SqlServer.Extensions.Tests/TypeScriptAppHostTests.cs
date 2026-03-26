using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.SqlServer.Extensions.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.SqlServer.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.SqlServer.Extensions",
            exampleName: "sqlserver-ext",
            waitForResources: ["sqlserver"],
            waitStatus: "up",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
