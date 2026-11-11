using CommunityToolkit.Aspire.Testing;

namespace CommunityToolkit.Aspire.Hosting.Flagd.Tests;

public class GoAppHostTests
{
    [Fact]
    public async Task GoAppHostCompiles()
    {
        await GoAppHostTest.Run(
            appHostProject: "CommunityToolkit.Aspire.Hosting.Flagd.AppHost.Go",
            exampleName: "flagd",
            cancellationToken: TestContext.Current.CancellationToken);
    }
}
