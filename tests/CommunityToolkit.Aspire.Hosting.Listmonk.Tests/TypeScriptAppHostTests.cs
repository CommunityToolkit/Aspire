using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Listmonk.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Listmonk.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Listmonk",
            exampleName: "listmonk",
            waitForResources: ["postgres", "listmonk", "listmonk-default-postgres", "listmonk-default"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
