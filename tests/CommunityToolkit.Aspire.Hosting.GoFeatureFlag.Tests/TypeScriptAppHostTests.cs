using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.GoFeatureFlag.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.GoFeatureFlag.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.GoFeatureFlag",
            exampleName: "goff",
            waitForResources: ["goff", "goff2"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}