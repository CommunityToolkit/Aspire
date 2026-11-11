using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Solr.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Solr.AppHost.Go",
            exampleName: "solr",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
