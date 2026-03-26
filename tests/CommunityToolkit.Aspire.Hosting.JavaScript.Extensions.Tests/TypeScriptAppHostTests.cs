using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.JavaScript.Extensions.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.JavaScript.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.JavaScript.Extensions",
            exampleName: "javascript-ext",
            waitForResources: ["turbo-web"],
            requiredCommands: ["yarn", "pnpm"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}