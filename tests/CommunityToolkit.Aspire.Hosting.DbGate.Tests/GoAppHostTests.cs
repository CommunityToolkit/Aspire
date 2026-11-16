using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.DbGate.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.DbGate.AppHost.Go",
            exampleName: "dbgate",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
