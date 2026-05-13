using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Solr.Tests;

[RequiresDocker]
public class TypeScriptAppHostTests
{
    [Fact]
    public async Task TypeScriptAppHostCompilesAndStarts()
    {
        await TypeScriptAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Solr.AppHost.TypeScript",
            packageName: "CommunityToolkit.Aspire.Hosting.Solr",
            exampleName: "solr",
            waitForResources: ["solr", "solr-bind"],
            cancellationToken: TestContext.Current.CancellationToken);
    }
}