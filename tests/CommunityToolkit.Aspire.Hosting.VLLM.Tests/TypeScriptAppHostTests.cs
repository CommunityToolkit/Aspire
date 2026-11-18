using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.VLLM.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.VLLM.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.VLLM",
            exampleName: "vllm",
            waitForResources: ["vllm"],
            waitTimeoutSeconds: 900,
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
