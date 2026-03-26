using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.MongoDB.Extensions.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.MongoDB.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.MongoDB.Extensions",
            exampleName: "mongodb-ext",
            waitForResources: ["mongo", "dbgate", "mongo-named"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}