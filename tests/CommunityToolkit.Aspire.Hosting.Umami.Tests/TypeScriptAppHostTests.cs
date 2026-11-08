using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Umami.Tests;

[RequiresContainerRuntime]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Umami.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Umami",
            exampleName: "umami",
            waitForResources: ["postgres", "umami", "umami-default"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
