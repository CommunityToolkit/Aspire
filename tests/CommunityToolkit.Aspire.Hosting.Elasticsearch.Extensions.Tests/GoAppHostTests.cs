using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Elasticsearch.Extensions.AppHost.Go",
            exampleName: "elasticsearch-ext",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
