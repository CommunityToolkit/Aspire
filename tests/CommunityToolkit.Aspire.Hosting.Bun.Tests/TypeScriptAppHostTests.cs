using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Bun.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Bun.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Bun",
            exampleName: "bun",
            waitForResources: ["bun-app", "bun-defaults", "bun-watch"],
            requiredCommands: ["bun"],
            waitStatus: "up",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
