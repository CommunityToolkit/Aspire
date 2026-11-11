using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Loki.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Loki.AppHost.Go",
            exampleName: "loki",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
