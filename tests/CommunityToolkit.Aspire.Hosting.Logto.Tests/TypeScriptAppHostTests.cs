using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Logto.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Logto.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Logto",
            exampleName: "logto",
            waitForResources: ["postgres", "redis", "logto"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
