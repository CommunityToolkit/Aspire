using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.McpInspector.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.McpInspector.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.McpInspector",
            exampleName: "mcp-inspector",
            waitForResources: ["inspector-default", "inspector-configured", "inspector-yarn", "inspector-pnpm", "inspector-bun"],
            requiredCommands: ["yarn", "pnpm", "bun"],
            secrets: new Dictionary<string, string>
            {
                ["Parameters:proxy-token"] = "test-proxy-token"
            },
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
