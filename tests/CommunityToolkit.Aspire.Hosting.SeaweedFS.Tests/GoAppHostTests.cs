using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.SeaweedFS.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "SeaweedFS.AppHost.Go",
            exampleName: "seaweedfs",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
