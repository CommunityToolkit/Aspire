using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Golang.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Golang.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Golang",
            exampleName: "golang",
            waitForResources: ["golang-root", "golang-cmd"],
            waitStatus: "up",
            requiredCommands: ["go"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}