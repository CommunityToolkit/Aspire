using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Loki.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Loki.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Loki",
            exampleName: "loki",
            waitForResources: ["loki", "loki-mounted"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}