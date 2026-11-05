using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flare.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Flare.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Flare",
            exampleName: "flare",
            waitForResources: ["flare", "flare-api", "flare-alert-worker", "flare-dashboard", "apiservice"],
            waitTimeoutSeconds: 300,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
