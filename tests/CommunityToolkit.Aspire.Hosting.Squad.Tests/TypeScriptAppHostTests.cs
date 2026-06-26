using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Squad.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Squad",
            exampleName: "squad",
            waitForResources: ["research-squad", "dev-squad"],
            waitStatus: "up",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
