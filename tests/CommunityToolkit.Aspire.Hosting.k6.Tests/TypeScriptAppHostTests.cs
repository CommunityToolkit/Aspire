using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.k6.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.k6.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.k6",
            exampleName: "k6",
            waitForResources: ["k6-default", "k6-browser"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
