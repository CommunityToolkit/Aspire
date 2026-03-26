using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.KurrentDB.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.KurrentDB.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.KurrentDB",
            exampleName: "kurrentdb",
            waitForResources: ["kurrentdb", "kurrentdb2", "kurrentdb3"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}