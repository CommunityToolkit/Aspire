using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Ngrok.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Ngrok.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Ngrok",
            exampleName: "ngrok",
            waitForResources: ["upstream", "ngrok-parameter", "ngrok-value"],
            waitStatus: "up",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
