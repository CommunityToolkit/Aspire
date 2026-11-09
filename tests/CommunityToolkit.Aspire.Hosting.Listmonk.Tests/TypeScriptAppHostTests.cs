using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Listmonk.Tests;

[RequiresContainerRuntime]
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
