using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.K3s.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.K3s.AppHost.Go",
            exampleName: "k3s",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
