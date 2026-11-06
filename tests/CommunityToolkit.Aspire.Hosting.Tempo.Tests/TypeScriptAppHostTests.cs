using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Tempo.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Tempo.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Tempo",
            exampleName: "tempo",
            waitForResources: ["tempo", "tempo-mounted"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}