using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Deno.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Deno.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Deno",
            exampleName: "deno",
            waitForResources: ["vite-demo", "oak-demo"],
            requiredCommands: ["deno"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
