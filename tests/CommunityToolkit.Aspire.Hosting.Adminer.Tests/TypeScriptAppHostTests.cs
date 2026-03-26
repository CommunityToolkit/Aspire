using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Adminer.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Adminer.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Adminer",
            exampleName: "adminer",
            waitForResources: ["adminer"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
