using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Dapr.AzureExtensions.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact(Skip = "TypeScript apphost currently fails with capability error when referencing Dapr components from project resources.")]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Azure.Dapr.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Azure.Dapr",
            exampleName: "dapr",
            waitForResources: ["redisState", "servicea"],
            waitStatus: "up",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
