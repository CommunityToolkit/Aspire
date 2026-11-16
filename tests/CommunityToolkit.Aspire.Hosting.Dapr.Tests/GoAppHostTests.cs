using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Dapr.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Dapr.AppHost.Go",
            exampleName: "dapr",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
