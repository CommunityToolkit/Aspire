using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Squad.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Squad.AppHost.Go",
            exampleName: "squad",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
