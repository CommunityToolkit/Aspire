using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions.Tests;

public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions",
            exampleName: "elasticsearch-ext",
            waitForResources: [],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}