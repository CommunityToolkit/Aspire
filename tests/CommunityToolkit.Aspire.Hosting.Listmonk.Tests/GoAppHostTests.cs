using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Listmonk.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Listmonk.AppHost.Go",
            exampleName: "listmonk",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
