using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact(Skip = "TypeScript apphost currently fails with capability error when referencing Dapr components from project resources.")]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Dapr.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Dapr",
            exampleName: "dapr",
            waitForResources: ["redis", "servicea"],
            waitStatus: "up",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
