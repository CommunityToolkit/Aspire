using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flagd.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Flagd.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Flagd",
            exampleName: "flagd",
            waitForResources: ["flagd", "flagd-default"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
