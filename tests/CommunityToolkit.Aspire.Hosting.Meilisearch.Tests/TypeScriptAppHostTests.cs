using Aspire.Components.Common.Tests;
using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Meilisearch.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Meilisearch.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Meilisearch",
            exampleName: "meilisearch",
            waitForResources: ["search", "search-defaults"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
